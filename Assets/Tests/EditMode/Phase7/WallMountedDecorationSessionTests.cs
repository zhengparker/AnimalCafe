using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AnimalCafe.Content;
using AnimalCafe.Decoration;
using AnimalCafe.Layout;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AnimalCafe.Tests.Phase7
{
    public sealed class WallMountedDecorationSessionTests
    {
        private const string LeftSurfaceId = "wall.back-left";
        private const string RightSurfaceId = "wall.back-right";
        private const string DecorDefinitionId = "wall-decor.framed-leaf";
        private const string WindowDefinitionId = "window.canonical";

        [Test]
        public void Constructor_RejectsMissingDuplicateOrIncompleteDefinitionBindings()
        {
            using var fixture = new Fixture();

            Assert.Throws<ArgumentNullException>(() =>
                new WallMountedDecorationSession(null, fixture.Definitions));
            Assert.Throws<ArgumentNullException>(() =>
                new WallMountedDecorationSession(fixture.Layout, null));
            Assert.Throws<ArgumentException>(() =>
                new WallMountedDecorationSession(
                    fixture.Layout,
                    fixture.Definitions.Concat(new WallMountedDefinitionAsset[] { null })));

            var duplicate = fixture.CreateDefinition(
                DecorDefinitionId,
                "Duplicate",
                1,
                1);
            Assert.Throws<ArgumentException>(() =>
                new WallMountedDecorationSession(
                    fixture.Layout,
                    fixture.Definitions.Concat(new[] { duplicate })));

            var incomplete = fixture.CreateDefinition(
                "wall-decor.incomplete",
                "Incomplete",
                1,
                1,
                includePrefab: false);
            Assert.Throws<ArgumentException>(() =>
                new WallMountedDecorationSession(
                    fixture.Layout,
                    fixture.Definitions.Concat(new[] { incomplete })));
        }

        [Test]
        public void Constructor_FreezesDefinitionValuesAgainstLaterAssetMutation()
        {
            using var fixture = new Fixture();
            var source = fixture.Definition(DecorDefinitionId);
            var session = fixture.CreateSession();

            fixture.SetDefinition(
                source,
                definitionId: "wall-decor.changed",
                width: 2,
                height: 2);
            session.BeginNew(
                DecorDefinitionId,
                LeftSurfaceId,
                new WallSlotPosition(0, 0));

            Assert.That(session.ActivePreview.DefinitionId, Is.EqualTo(DecorDefinitionId));
            Assert.That(session.ActivePreview.Footprint.Width, Is.EqualTo(1));
            Assert.That(session.ActivePreview.Footprint.Height, Is.EqualTo(1));
        }

        [Test]
        public void BeginNew_SelectsNearestValidSlotOnPreferredSurfaceBeforeFallbackWalls()
        {
            using var fixture = new Fixture(
                leftColumns: 4,
                rightColumns: 4,
                rows: 2);
            fixture.Place(
                "block.exact-left",
                DecorDefinitionId,
                LeftSurfaceId,
                1,
                0);
            fixture.Place(
                "block.exact-right",
                DecorDefinitionId,
                RightSurfaceId,
                1,
                0);

            fixture.Session.BeginNew(
                DecorDefinitionId,
                RightSurfaceId,
                new WallSlotPosition(1, 0));

            Assert.That(fixture.Session.ActivePreview.SurfaceId, Is.EqualTo(RightSurfaceId));
            Assert.That(fixture.Session.ActivePreview.Position,
                Is.EqualTo(new WallSlotPosition(0, 0)));
            Assert.That(fixture.Session.ActivePreview.IsValid, Is.True);
            Assert.That(fixture.Layout.CaptureSnapshot().Instances.Count, Is.EqualTo(2));
        }

        [Test]
        public void BeginNew_PreferredSurfaceWinsExactTieRegardlessOfInputOrder()
        {
            using var fixture = new Fixture();
            var first = fixture.CreateSession(surfaceOrderReversed: false);
            var second = fixture.CreateSession(surfaceOrderReversed: true);

            first.BeginNew(
                DecorDefinitionId,
                RightSurfaceId,
                new WallSlotPosition(2, 1));
            second.BeginNew(
                DecorDefinitionId,
                RightSurfaceId,
                new WallSlotPosition(2, 1));

            Assert.That(first.ActivePreview.SurfaceId, Is.EqualTo(RightSurfaceId));
            Assert.That(second.ActivePreview.SurfaceId, Is.EqualTo(RightSurfaceId));
            Assert.That(first.ActivePreview.Position,
                Is.EqualTo(new WallSlotPosition(2, 1)));
            Assert.That(second.ActivePreview.Position, Is.EqualTo(first.ActivePreview.Position));
        }

        [TestCase(17)]
        [TestCase(20260825)]
        [TestCase(8675309)]
        public void BeginNew_RandomizedSurfaceInputOrderAlwaysProducesSameWinner(
            int seed)
        {
            using var fixture = new Fixture();
            var random = new System.Random(seed);
            var normalOrderCount = 0;
            var reversedOrderCount = 0;

            for (var iteration = 0; iteration < 32; iteration++)
            {
                var left = new WallSurfaceLayout(LeftSurfaceId, 8, 2);
                var right = new WallSurfaceLayout(RightSurfaceId, 8, 2);
                var usesNormalOrder = random.Next(2) == 0;
                var surfaces = usesNormalOrder
                    ? new[] { left, right }
                    : new[] { right, left };
                if (usesNormalOrder)
                {
                    normalOrderCount++;
                }
                else
                {
                    reversedOrderCount++;
                }
                var session = new WallMountedDecorationSession(
                    new WallMountedLayout(surfaces),
                    fixture.Definitions);

                session.BeginNew(
                    DecorDefinitionId,
                    RightSurfaceId,
                    new WallSlotPosition(2, 1));

                Assert.That(session.ActivePreview.SurfaceId,
                    Is.EqualTo(RightSurfaceId),
                    $"seed={seed}, iteration={iteration}");
                Assert.That(session.ActivePreview.Position,
                    Is.EqualTo(new WallSlotPosition(2, 1)),
                    $"seed={seed}, iteration={iteration}");
            }

            Assert.That(normalOrderCount, Is.GreaterThan(0), $"seed={seed}");
            Assert.That(reversedOrderCount, Is.GreaterThan(0), $"seed={seed}");
        }

        [Test]
        public void BeginNew_ColumnComparatorChoosesLowerColumnOnEqualDistance()
        {
            using var fixture = new Fixture();
            var surface = new WallSurfaceLayout("wall.column-tie", 3, 1);
            var layout = new WallMountedLayout(new[] { surface });
            Assert.That(layout.Place(new WallMountedInstance(
                "block.preferred",
                DecorDefinitionId,
                "wall.column-tie",
                new WallSlotPosition(1, 0),
                new WallFootprint(1, 1))).Succeeded, Is.True);
            var session = new WallMountedDecorationSession(layout, fixture.Definitions);

            session.BeginNew(
                DecorDefinitionId,
                "wall.column-tie",
                new WallSlotPosition(1, 0));

            Assert.That(session.ActivePreview.Position,
                Is.EqualTo(new WallSlotPosition(0, 0)));
        }

        [Test]
        public void BeginNew_RowComparatorChoosesLowerRowOnEqualDistanceInPureThreeRowFixture()
        {
            using var fixture = new Fixture();
            var surface = new WallSurfaceLayout("wall.row-tie", 1, 3);
            var layout = new WallMountedLayout(new[] { surface });
            Assert.That(layout.Place(new WallMountedInstance(
                "block.preferred",
                DecorDefinitionId,
                "wall.row-tie",
                new WallSlotPosition(0, 1),
                new WallFootprint(1, 1))).Succeeded, Is.True);
            var session = new WallMountedDecorationSession(layout, fixture.Definitions);

            session.BeginNew(
                DecorDefinitionId,
                "wall.row-tie",
                new WallSlotPosition(0, 1));

            Assert.That(session.ActivePreview.Position,
                Is.EqualTo(new WallSlotPosition(0, 0)));
        }

        [Test]
        public void BeginNew_WhenNoValidSlot_PreservesRequestedInvalidTarget()
        {
            using var fixture = new Fixture(leftColumns: 1, rightColumns: 1, rows: 1);
            fixture.Place("block.left", DecorDefinitionId, LeftSurfaceId, 0, 0);
            fixture.Place("block.right", DecorDefinitionId, RightSurfaceId, 0, 0);

            fixture.Session.BeginNew(
                DecorDefinitionId,
                RightSurfaceId,
                new WallSlotPosition(0, 0));

            Assert.That(fixture.Session.ActivePreview.SurfaceId, Is.EqualTo(RightSurfaceId));
            Assert.That(fixture.Session.ActivePreview.Position,
                Is.EqualTo(new WallSlotPosition(0, 0)));
            Assert.That(fixture.Session.ActivePreview.IsValid, Is.False);
            Assert.That(fixture.Session.ActivePreview.FailureReason,
                Is.EqualTo(WallPlacementFailureReason.Overlap));
        }

        [Test]
        public void BeginNew_RejectsUnknownDefinitionAndSecondActiveBegin()
        {
            using var fixture = new Fixture();

            Assert.Throws<ArgumentException>(() => fixture.Session.BeginNew(
                "wall-decor.unknown",
                LeftSurfaceId,
                new WallSlotPosition(0, 0)));

            fixture.Session.BeginNew(
                DecorDefinitionId,
                LeftSurfaceId,
                new WallSlotPosition(0, 0));
            var held = fixture.Session.ActivePreview;

            Assert.Throws<InvalidOperationException>(() => fixture.Session.BeginNew(
                WindowDefinitionId,
                RightSurfaceId,
                new WallSlotPosition(0, 0)));
            Assert.That(fixture.Session.ActivePreview, Is.SameAs(held));
        }

        [Test]
        public void ActivePreview_IsPointInTimeAndMoveDoesNotMutateConfirmedLayout()
        {
            using var fixture = new Fixture();
            fixture.Session.BeginNew(
                DecorDefinitionId,
                LeftSurfaceId,
                new WallSlotPosition(0, 0));
            var originalView = fixture.Session.ActivePreview;
            var before = Snapshot(fixture.Layout);

            var result = fixture.Session.MovePreview(
                RightSurfaceId,
                new WallSlotPosition(3, 1));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(fixture.Session.ActivePreview, Is.Not.SameAs(originalView));
            Assert.That(originalView.SurfaceId, Is.EqualTo(LeftSurfaceId));
            Assert.That(originalView.Position, Is.EqualTo(new WallSlotPosition(0, 0)));
            Assert.That(fixture.Session.ActivePreview.SurfaceId, Is.EqualTo(RightSurfaceId));
            Assert.That(fixture.Session.ActivePreview.Position,
                Is.EqualTo(new WallSlotPosition(3, 1)));
            Assert.That(Snapshot(fixture.Layout), Is.EqualTo(before));
        }

        [Test]
        public void NewPreview_HasNoStoreOrRotateAndConfirmCreatesExactlyOneInstance()
        {
            using var fixture = new Fixture();
            fixture.Session.BeginNew(
                DecorDefinitionId,
                LeftSurfaceId,
                new WallSlotPosition(0, 0));

            Assert.That(fixture.Session.ActivePreview.InstanceId, Is.Null);
            Assert.That(fixture.Session.ActivePreview.IsExisting, Is.False);
            Assert.That(fixture.Session.ActivePreview.CanConfirm, Is.True);
            Assert.That(fixture.Session.BeginStoreConfirmation(), Is.False);
            Assert.That(
                typeof(WallMountedDecorationSession).GetMethod(
                    "RotatePreview",
                    BindingFlags.Public | BindingFlags.Instance),
                Is.Null);

            var result = fixture.Session.ConfirmPreview();

            Assert.That(result.Succeeded, Is.True);
            Assert.That(fixture.Session.ActivePreview, Is.Null);
            var instances = fixture.Layout.CaptureSnapshot().Instances;
            Assert.That(instances.Count, Is.EqualTo(1));
            Assert.That(instances[0].DefinitionId, Is.EqualTo(DecorDefinitionId));
        }

        [Test]
        public void BeginExisting_MoveThenCancel_PreservesExactConfirmedState()
        {
            using var fixture = new Fixture();
            fixture.Place(
                "decor.existing",
                DecorDefinitionId,
                LeftSurfaceId,
                1,
                0);
            var before = Snapshot(fixture.Layout);

            var begin = fixture.Session.BeginExisting("decor.existing");
            var move = fixture.Session.MovePreview(
                RightSurfaceId,
                new WallSlotPosition(2, 1));
            fixture.Session.CancelPreview();

            Assert.That(begin.Succeeded, Is.True);
            Assert.That(move.Succeeded, Is.True);
            Assert.That(fixture.Session.ActivePreview, Is.Null);
            Assert.That(Snapshot(fixture.Layout), Is.EqualTo(before));
        }

        [Test]
        public void ExistingPreview_SameAndCrossWallMovesKeepSourceUntilAtomicConfirm()
        {
            using var fixture = new Fixture();
            fixture.Place(
                "decor.existing",
                DecorDefinitionId,
                LeftSurfaceId,
                1,
                0);
            var before = Snapshot(fixture.Layout);

            Assert.That(fixture.Session.BeginExisting("decor.existing").Succeeded, Is.True);
            Assert.That(fixture.Session.MovePreview(
                LeftSurfaceId,
                new WallSlotPosition(2, 0)).Succeeded, Is.True);
            Assert.That(Snapshot(fixture.Layout), Is.EqualTo(before));
            Assert.That(fixture.Session.MovePreview(
                RightSurfaceId,
                new WallSlotPosition(3, 1)).Succeeded, Is.True);
            Assert.That(Snapshot(fixture.Layout), Is.EqualTo(before));

            var confirm = fixture.Session.ConfirmPreview();

            Assert.That(confirm.Succeeded, Is.True);
            Assert.That(fixture.Layout.TryGetInstance("decor.existing", out var moved), Is.True);
            Assert.That(moved.InstanceId, Is.EqualTo("decor.existing"));
            Assert.That(moved.SurfaceId, Is.EqualTo(RightSurfaceId));
            Assert.That(moved.Position, Is.EqualTo(new WallSlotPosition(3, 1)));
        }

        [TestCase(null, WallPlacementFailureReason.CrossCorner,
            PlacementFeedbackKey.WallCrossCorner)]
        [TestCase("", WallPlacementFailureReason.CrossCorner,
            PlacementFeedbackKey.WallCrossCorner)]
        [TestCase("wall.missing", WallPlacementFailureReason.SurfaceMissing,
            PlacementFeedbackKey.WallSurfaceMissing)]
        public void InvalidSurfaceTarget_DisablesAndRejectsDirectConfirm(
            string surfaceId,
            WallPlacementFailureReason expectedReason,
            PlacementFeedbackKey expectedFeedback)
        {
            using var fixture = new Fixture();
            fixture.Session.BeginNew(
                DecorDefinitionId,
                LeftSurfaceId,
                new WallSlotPosition(0, 0));
            var before = Snapshot(fixture.Layout);

            var move = fixture.Session.MovePreview(
                surfaceId,
                new WallSlotPosition(0, 0));
            var confirm = fixture.Session.ConfirmPreview();

            Assert.That(move.Succeeded, Is.False);
            Assert.That(move.FailureReason, Is.EqualTo(expectedReason));
            Assert.That(fixture.Session.ActivePreview.IsValid, Is.False);
            Assert.That(fixture.Session.ActivePreview.CanConfirm, Is.False);
            Assert.That(fixture.Session.ActivePreview.FailureReason, Is.EqualTo(expectedReason));
            Assert.That(PlacementFeedbackMapper.Map(move), Is.EqualTo(expectedFeedback));
            Assert.That(confirm.Succeeded, Is.False);
            Assert.That(confirm.FailureReason, Is.EqualTo(expectedReason));
            Assert.That(Snapshot(fixture.Layout), Is.EqualTo(before));
        }

        [Test]
        public void OutOfBoundsPreview_MapsExactFeedbackAndRejectsDirectConfirm()
        {
            using var fixture = new Fixture();
            fixture.Session.BeginNew(
                DecorDefinitionId,
                LeftSurfaceId,
                new WallSlotPosition(0, 0));
            var before = Snapshot(fixture.Layout);

            var move = fixture.Session.MovePreview(
                RightSurfaceId,
                new WallSlotPosition(8, 0));
            var confirm = fixture.Session.ConfirmPreview();

            Assert.That(move.FailureReason,
                Is.EqualTo(WallPlacementFailureReason.OutOfBounds));
            Assert.That(PlacementFeedbackMapper.Map(move),
                Is.EqualTo(PlacementFeedbackKey.WallOutOfBounds));
            Assert.That(fixture.Session.ActivePreview.CanConfirm, Is.False);
            Assert.That(confirm.FailureReason,
                Is.EqualTo(WallPlacementFailureReason.OutOfBounds));
            Assert.That(Snapshot(fixture.Layout), Is.EqualTo(before));
        }

        [Test]
        public void OverlapPreview_MapsExactFeedbackAndKeepsExistingSourceConfirmed()
        {
            using var fixture = new Fixture();
            fixture.Place("decor.source", DecorDefinitionId, LeftSurfaceId, 0, 0);
            fixture.Place("window.blocker", WindowDefinitionId, RightSurfaceId, 3, 1);
            fixture.Session.BeginExisting("decor.source");
            var before = Snapshot(fixture.Layout);

            var move = fixture.Session.MovePreview(
                RightSurfaceId,
                new WallSlotPosition(3, 1));
            var confirm = fixture.Session.ConfirmPreview();

            Assert.That(move.FailureReason, Is.EqualTo(WallPlacementFailureReason.Overlap));
            Assert.That(PlacementFeedbackMapper.Map(move),
                Is.EqualTo(PlacementFeedbackKey.WallOverlap));
            Assert.That(fixture.Session.ActivePreview.CanConfirm, Is.False);
            Assert.That(confirm.FailureReason, Is.EqualTo(WallPlacementFailureReason.Overlap));
            Assert.That(Snapshot(fixture.Layout), Is.EqualTo(before));
            Assert.That(fixture.Layout.TryGetInstance("decor.source", out _), Is.True);
        }

        [Test]
        public void StoreConfirmation_DismissRestoresEditingWithoutAnyMutation()
        {
            using var fixture = new Fixture();
            fixture.Place("decor.existing", DecorDefinitionId, LeftSurfaceId, 0, 0);
            fixture.Session.BeginExisting("decor.existing");
            fixture.Session.MovePreview(RightSurfaceId, new WallSlotPosition(2, 1));
            var beforeLayout = Snapshot(fixture.Layout);
            var beforePreview = fixture.Session.ActivePreview;

            Assert.That(fixture.Session.BeginStoreConfirmation(), Is.True);
            Assert.That(fixture.Session.ActivePreview.IsStoreConfirmationPending, Is.True);
            fixture.Session.DismissStoreConfirmation();

            Assert.That(fixture.Session.ActivePreview.IsStoreConfirmationPending, Is.False);
            Assert.That(fixture.Session.ActivePreview.DefinitionId,
                Is.EqualTo(beforePreview.DefinitionId));
            Assert.That(fixture.Session.ActivePreview.InstanceId,
                Is.EqualTo(beforePreview.InstanceId));
            Assert.That(fixture.Session.ActivePreview.SurfaceId,
                Is.EqualTo(beforePreview.SurfaceId));
            Assert.That(fixture.Session.ActivePreview.Position,
                Is.EqualTo(beforePreview.Position));
            Assert.That(Snapshot(fixture.Layout), Is.EqualTo(beforeLayout));
        }

        [Test]
        public void StoreConfirmationPending_BlocksMoveAndDirectConfirmWithoutAnyMutation()
        {
            using var fixture = new Fixture();
            fixture.Place("decor.existing", DecorDefinitionId, LeftSurfaceId, 0, 0);
            Assert.That(fixture.Session.BeginExisting("decor.existing").Succeeded, Is.True);
            Assert.That(fixture.Session.BeginStoreConfirmation(), Is.True);
            var pendingPreview = fixture.Session.ActivePreview;
            var beforeLayout = Snapshot(fixture.Layout);

            var move = fixture.Session.MovePreview(
                RightSurfaceId,
                new WallSlotPosition(2, 1));
            var confirm = fixture.Session.ConfirmPreview();

            Assert.That(move.Succeeded, Is.False);
            Assert.That(move.FailureReason,
                Is.EqualTo(WallPlacementFailureReason.ConfirmationPending));
            Assert.That(confirm.Succeeded, Is.False);
            Assert.That(confirm.FailureReason,
                Is.EqualTo(WallPlacementFailureReason.ConfirmationPending));
            Assert.That(fixture.Session.ActivePreview, Is.SameAs(pendingPreview));
            Assert.That(fixture.Session.ActivePreview.IsStoreConfirmationPending, Is.True);
            Assert.That(Snapshot(fixture.Layout), Is.EqualTo(beforeLayout));
        }

        [Test]
        public void ConfirmStore_RemovesOnceReleasesSlotsAndDefinitionRemainsReusable()
        {
            using var fixture = new Fixture();
            var originalPosition = new WallSlotPosition(0, 0);
            var footprint = new WallFootprint(1, 1);
            fixture.Place("decor.existing", DecorDefinitionId, LeftSurfaceId, 0, 0);
            var beforeSurface = fixture.Layout.Surfaces[LeftSurfaceId];
            Assert.That(beforeSurface.TryGetOccupant(
                originalPosition,
                out var originalOwner), Is.True);
            Assert.That(originalOwner, Is.EqualTo("decor.existing"));
            var beforeOccupiedCount = beforeSurface.OccupiedSlotCount;
            fixture.Session.BeginExisting("decor.existing");
            Assert.That(fixture.Session.BeginStoreConfirmation(), Is.True);

            var first = fixture.Session.ConfirmStore();
            var afterFirst = Snapshot(fixture.Layout);
            var afterSurface = fixture.Layout.Surfaces[LeftSurfaceId];
            var second = fixture.Session.ConfirmStore();

            Assert.That(first.Succeeded, Is.True);
            Assert.That(fixture.Session.ActivePreview, Is.Null);
            Assert.That(fixture.Layout.TryGetInstance("decor.existing", out _), Is.False);
            Assert.That(afterSurface.OccupiedSlotCount,
                Is.EqualTo(beforeOccupiedCount - footprint.Width * footprint.Height));
            Assert.That(afterSurface.TryGetOccupant(originalPosition, out _), Is.False);
            Assert.That(fixture.Layout.ValidatePlacement(
                DecorDefinitionId,
                LeftSurfaceId,
                originalPosition,
                footprint).Succeeded, Is.True);
            Assert.That(second.Succeeded, Is.False);
            Assert.That(second.FailureReason,
                Is.EqualTo(WallPlacementFailureReason.ItemNotFound));
            Assert.That(Snapshot(fixture.Layout), Is.EqualTo(afterFirst));

            fixture.Session.BeginNew(
                DecorDefinitionId,
                LeftSurfaceId,
                originalPosition);
            Assert.That(fixture.Session.ActivePreview.IsValid, Is.True);
            Assert.That(fixture.Session.ActivePreview.SurfaceId, Is.EqualTo(LeftSurfaceId));
            Assert.That(fixture.Session.ActivePreview.Position, Is.EqualTo(originalPosition));
        }

        [Test]
        public void NewPreview_StoreIsUnavailableAndDoesNotMutate()
        {
            using var fixture = new Fixture();
            fixture.Session.BeginNew(
                DecorDefinitionId,
                LeftSurfaceId,
                new WallSlotPosition(0, 0));
            var beforeLayout = Snapshot(fixture.Layout);
            var beforePreview = fixture.Session.ActivePreview;

            var began = fixture.Session.BeginStoreConfirmation();
            var result = fixture.Session.ConfirmStore();

            Assert.That(began, Is.False);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(fixture.Session.ActivePreview, Is.SameAs(beforePreview));
            Assert.That(fixture.Session.ActivePreview.IsStoreConfirmationPending, Is.False);
            Assert.That(Snapshot(fixture.Layout), Is.EqualTo(beforeLayout));
        }

        [TestCase(DecorDefinitionId)]
        [TestCase(WindowDefinitionId)]
        public void WallDecorAndWindow_ShareNewMoveCancelStoreOverlapAndBoundsLifecycle(
            string definitionId)
        {
            using var fixture = new Fixture();
            fixture.Place("blocker.shared", DecorDefinitionId, RightSurfaceId, 3, 1);

            fixture.Session.BeginNew(
                definitionId,
                LeftSurfaceId,
                new WallSlotPosition(1, 0));
            Assert.That(fixture.Session.MovePreview(
                RightSurfaceId,
                new WallSlotPosition(3, 1)).FailureReason,
                Is.EqualTo(WallPlacementFailureReason.Overlap));
            Assert.That(fixture.Session.MovePreview(
                RightSurfaceId,
                new WallSlotPosition(8, 0)).FailureReason,
                Is.EqualTo(WallPlacementFailureReason.OutOfBounds));
            Assert.That(fixture.Session.MovePreview(
                LeftSurfaceId,
                new WallSlotPosition(2, 1)).Succeeded, Is.True);
            Assert.That(fixture.Session.ConfirmPreview().Succeeded, Is.True);

            var instance = fixture.Layout.CaptureSnapshot().Instances.Single(
                item => item.DefinitionId == definitionId &&
                    item.InstanceId != "blocker.shared");
            Assert.That(fixture.Session.BeginExisting(instance.InstanceId).Succeeded, Is.True);
            fixture.Session.MovePreview(RightSurfaceId, new WallSlotPosition(2, 0));
            fixture.Session.CancelPreview();
            Assert.That(fixture.Layout.TryGetInstance(instance.InstanceId, out var cancelled), Is.True);
            Assert.That(cancelled.SurfaceId, Is.EqualTo(LeftSurfaceId));

            Assert.That(fixture.Session.BeginExisting(instance.InstanceId).Succeeded, Is.True);
            Assert.That(fixture.Session.BeginStoreConfirmation(), Is.True);
            Assert.That(fixture.Session.ConfirmStore().Succeeded, Is.True);
            Assert.That(fixture.Layout.TryGetInstance(instance.InstanceId, out _), Is.False);
        }

        [Test]
        public void ExistingAndMissingBegin_ReturnExpectedIdentityOrFailure()
        {
            using var fixture = new Fixture();
            fixture.Place("window.existing", WindowDefinitionId, RightSurfaceId, 4, 0);

            var missing = fixture.Session.BeginExisting("window.missing");
            var found = fixture.Session.BeginExisting("window.existing");

            Assert.That(missing.FailureReason,
                Is.EqualTo(WallPlacementFailureReason.ItemNotFound));
            Assert.That(found.Succeeded, Is.True);
            Assert.That(fixture.Session.ActivePreview.InstanceId,
                Is.EqualTo("window.existing"));
            Assert.That(fixture.Session.ActivePreview.IsExisting, Is.True);
            Assert.That(fixture.Session.ActivePreview.DefinitionId,
                Is.EqualTo(WindowDefinitionId));
            Assert.That(fixture.Session.ActivePreview.SurfaceId, Is.EqualTo(RightSurfaceId));
            Assert.That(fixture.Session.ActivePreview.Position,
                Is.EqualTo(new WallSlotPosition(4, 0)));
        }

        [Test]
        public void BeginExisting_RejectsSecondBeginWhilePreviewIsActiveWithoutMutation()
        {
            using var fixture = new Fixture();
            fixture.Place("decor.first", DecorDefinitionId, LeftSurfaceId, 0, 0);
            fixture.Place("window.second", WindowDefinitionId, RightSurfaceId, 4, 0);
            Assert.That(fixture.Session.BeginExisting("decor.first").Succeeded, Is.True);
            var held = fixture.Session.ActivePreview;
            var before = Snapshot(fixture.Layout);

            Assert.Throws<InvalidOperationException>(() =>
                fixture.Session.BeginExisting("window.second"));

            Assert.That(fixture.Session.ActivePreview, Is.SameAs(held));
            Assert.That(fixture.Session.ActivePreview.InstanceId, Is.EqualTo("decor.first"));
            Assert.That(Snapshot(fixture.Layout), Is.EqualTo(before));
        }

        [Test]
        public void PlacementFeedbackMapper_PreservesEveryPhase6Mapping()
        {
            var expected = new Dictionary<PlacementFailureReason, PlacementFeedbackKey>
            {
                { PlacementFailureReason.None, PlacementFeedbackKey.None },
                { PlacementFailureReason.Overlap, PlacementFeedbackKey.Occupied },
                { PlacementFailureReason.OutOfUnlockedRegion, PlacementFeedbackKey.OutsideUnlockedArea },
                { PlacementFailureReason.OutOfLayoutBounds, PlacementFeedbackKey.OutsideUnlockedArea },
                { PlacementFailureReason.LockedCell, PlacementFeedbackKey.Locked },
                { PlacementFailureReason.Blocked, PlacementFeedbackKey.Blocked },
                { PlacementFailureReason.ReservedEntranceClearance, PlacementFeedbackKey.EntranceClearance },
                { PlacementFailureReason.UnsupportedPlacementSurface, PlacementFeedbackKey.UnsupportedSurface },
                { PlacementFailureReason.InstanceNotFound, PlacementFeedbackKey.MissingInstance },
                { PlacementFailureReason.InstanceAlreadyPlaced, PlacementFeedbackKey.MissingInstance }
            };

            foreach (var pair in expected)
            {
                var result = pair.Key == PlacementFailureReason.None
                    ? PlacementResult.Success()
                    : PlacementResult.Failure(pair.Key);
                Assert.That(PlacementFeedbackMapper.Map(result),
                    Is.EqualTo(pair.Value),
                    pair.Key.ToString());
            }
        }

        private static string Snapshot(WallMountedLayout layout)
        {
            return JsonUtility.ToJson(layout.CaptureSnapshot());
        }

        private sealed class Fixture : IDisposable
        {
            private readonly List<UnityEngine.Object> ownedObjects =
                new List<UnityEngine.Object>();
            private readonly int leftColumns;
            private readonly int rightColumns;
            private readonly int rows;

            public WallMountedLayout Layout { get; private set; }
            public WallMountedDecorationSession Session { get; private set; }
            public IReadOnlyList<WallMountedDefinitionAsset> Definitions { get; }

            public Fixture(int leftColumns = 8, int rightColumns = 8, int rows = 2)
            {
                this.leftColumns = leftColumns;
                this.rightColumns = rightColumns;
                this.rows = rows;
                Definitions = new[]
                {
                    CreateDefinition(DecorDefinitionId, "Framed Leaf", 1, 1),
                    CreateDefinition(WindowDefinitionId, "Window", 1, 1)
                };
                Layout = CreateLayout(false);
                Session = new WallMountedDecorationSession(Layout, Definitions);
            }

            public WallMountedDecorationSession CreateSession(bool surfaceOrderReversed = false)
            {
                Layout = CreateLayout(surfaceOrderReversed);
                Session = new WallMountedDecorationSession(Layout, Definitions);
                return Session;
            }

            public WallMountedDefinitionAsset Definition(string definitionId)
            {
                return Definitions.Single(definition =>
                    definition.DefinitionId == definitionId);
            }

            public WallMountedDefinitionAsset CreateDefinition(
                string definitionId,
                string displayName,
                int width,
                int height,
                bool includePrefab = true)
            {
                var definition = ScriptableObject.CreateInstance<WallMountedDefinitionAsset>();
                var prefab = includePrefab ? new GameObject(definitionId + ".prefab") : null;
                var texture = new Texture2D(1, 1);
                var thumbnail = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, 1f, 1f),
                    Vector2.zero);
                ownedObjects.Add(definition);
                if (prefab != null)
                {
                    ownedObjects.Add(prefab);
                }
                ownedObjects.Add(thumbnail);
                ownedObjects.Add(texture);

                var serialized = new SerializedObject(definition);
                serialized.FindProperty("definitionId").stringValue = definitionId;
                serialized.FindProperty("displayName").stringValue = displayName;
                serialized.FindProperty("footprintWidth").intValue = width;
                serialized.FindProperty("footprintHeight").intValue = height;
                serialized.FindProperty("prefab").objectReferenceValue = prefab;
                serialized.FindProperty("thumbnail").objectReferenceValue = thumbnail;
                serialized.FindProperty("maxVisualDepth").floatValue = 0.2f;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                return definition;
            }

            public void SetDefinition(
                WallMountedDefinitionAsset definition,
                string definitionId = null,
                int? width = null,
                int? height = null)
            {
                var serialized = new SerializedObject(definition);
                if (definitionId != null)
                {
                    serialized.FindProperty("definitionId").stringValue = definitionId;
                }
                if (width.HasValue)
                {
                    serialized.FindProperty("footprintWidth").intValue = width.Value;
                }
                if (height.HasValue)
                {
                    serialized.FindProperty("footprintHeight").intValue = height.Value;
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            public void Place(
                string instanceId,
                string definitionId,
                string surfaceId,
                int column,
                int row,
                int width = 1,
                int height = 1)
            {
                var result = Layout.Place(new WallMountedInstance(
                    instanceId,
                    definitionId,
                    surfaceId,
                    new WallSlotPosition(column, row),
                    new WallFootprint(width, height)));
                Assert.That(result.Succeeded, Is.True, "Fixture placement must succeed.");
            }

            public void Dispose()
            {
                for (var index = ownedObjects.Count - 1; index >= 0; index--)
                {
                    UnityEngine.Object.DestroyImmediate(ownedObjects[index]);
                }
            }

            private WallMountedLayout CreateLayout(bool reverse)
            {
                var left = new WallSurfaceLayout(LeftSurfaceId, leftColumns, rows);
                var right = new WallSurfaceLayout(RightSurfaceId, rightColumns, rows);
                return new WallMountedLayout(reverse
                    ? new[] { right, left }
                    : new[] { left, right });
            }
        }
    }
}

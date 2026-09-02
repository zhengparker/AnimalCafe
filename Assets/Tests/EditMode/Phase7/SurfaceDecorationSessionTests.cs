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
    public sealed class SurfaceDecorationSessionTests
    {
        [Test]
        [Category("Normal")]
        public void WallPreview_AllowsBaseAndWainscotingInOneTransaction()
        {
            using var fixture = new Fixture();
            var confirmed = fixture.Layout;

            Assert.That(fixture.Session.BeginWall("wall.back-left").Succeeded, Is.True);
            Assert.That(fixture.Session.SelectStyle("wallpaper.cream-floral").Succeeded, Is.True);
            Assert.That(fixture.Session.SelectStyle("wainscoting.sage-plain").Succeeded, Is.True);
            Assert.That(fixture.Session.ActivePreview.HasChanges, Is.True);
            Assert.That(confirmed.TryGetWall("wall.back-left", out var before), Is.True);
            Assert.That(before.BaseStyleId, Is.Not.EqualTo("wallpaper.cream-floral"));

            var proposed = Wall(fixture.Session.ActivePreview.ProposedSnapshot, "wall.back-left");
            Assert.That(proposed.BaseStyleId, Is.EqualTo("wallpaper.cream-floral"));
            Assert.That(proposed.WainscotingStyleId, Is.EqualTo("wainscoting.sage-plain"));
            Assert.That(fixture.Session.ActivePreview.UsingWallBaseStyleId,
                Is.EqualTo("paint.cream"));
            Assert.That(fixture.Session.ActivePreview.PreviewWallBaseStyleId,
                Is.EqualTo("wallpaper.cream-floral"));
            Assert.That(fixture.Session.ActivePreview.UsingWallWainscotingStyleId,
                Is.EqualTo("wains.raised"));
            Assert.That(fixture.Session.ActivePreview.PreviewWallWainscotingStyleId,
                Is.EqualTo("wainscoting.sage-plain"));
            var previewBeforeApplyAll = SnapshotJson(fixture.Session.ActivePreview.ProposedSnapshot);
            var confirmedBeforeApplyAll = SnapshotJson(confirmed);
            var hadChangesBeforeApplyAll = fixture.Session.ActivePreview.HasChanges;
            var couldUndoBeforeApplyAll = fixture.Session.ActivePreview.CanUndo;
            var previewBaseBeforeApplyAll = fixture.Session.ActivePreview.PreviewWallBaseStyleId;
            var previewWainsBeforeApplyAll = fixture.Session.ActivePreview.PreviewWallWainscotingStyleId;

            AssertFailure(fixture.Session.ApplyAll(), SurfaceSessionFailure.WrongStyleKind);

            Assert.That(SnapshotJson(fixture.Session.ActivePreview.ProposedSnapshot),
                Is.EqualTo(previewBeforeApplyAll));
            Assert.That(SnapshotJson(confirmed), Is.EqualTo(confirmedBeforeApplyAll));
            Assert.That(fixture.Session.ActivePreview.HasChanges,
                Is.EqualTo(hadChangesBeforeApplyAll));
            Assert.That(fixture.Session.ActivePreview.CanUndo,
                Is.EqualTo(couldUndoBeforeApplyAll));
            Assert.That(fixture.Session.ActivePreview.PreviewWallBaseStyleId,
                Is.EqualTo(previewBaseBeforeApplyAll));
            Assert.That(fixture.Session.ActivePreview.PreviewWallWainscotingStyleId,
                Is.EqualTo(previewWainsBeforeApplyAll));

            Assert.That(fixture.Session.UndoLast(), Is.True);
            var afterWainscotingUndo = Wall(
                fixture.Session.ActivePreview.ProposedSnapshot, "wall.back-left");
            Assert.That(afterWainscotingUndo.BaseStyleId,
                Is.EqualTo("wallpaper.cream-floral"));
            Assert.That(afterWainscotingUndo.WainscotingStyleId,
                Is.EqualTo("wains.raised"));
            Assert.That(fixture.Session.ActivePreview.HasChanges, Is.True);
            Assert.That(fixture.Session.ActivePreview.CanUndo, Is.True);

            Assert.That(fixture.Session.UndoLast(), Is.True);
            var afterBaseUndo = Wall(
                fixture.Session.ActivePreview.ProposedSnapshot, "wall.back-left");
            Assert.That(afterBaseUndo.BaseStyleId, Is.EqualTo("paint.cream"));
            Assert.That(afterBaseUndo.WainscotingStyleId, Is.EqualTo("wains.raised"));
            Assert.That(fixture.Session.ActivePreview.HasChanges, Is.False);
            Assert.That(fixture.Session.ActivePreview.CanUndo, Is.False);
            Assert.That(fixture.Session.UndoLast(), Is.False);
        }

        [Test]
        [Category("Boundary")]
        public void WallPreview_RetargetsOnlyBeforeChanges()
        {
            using var fixture = new Fixture();
            AssertSucceeded(fixture.Session.BeginWall("wall.back-left"));
            AssertSucceeded(fixture.Session.BeginWall("wall.back-right"));
            Assert.That(fixture.Session.ActivePreview.TargetWallSurfaceId,
                Is.EqualTo("wall.back-right"));

            AssertSucceeded(fixture.Session.SelectStyle("wallpaper.cream-floral"));
            var beforeRejectedRetarget = SnapshotJson(fixture.Session.ActivePreview.ProposedSnapshot);
            AssertFailure(fixture.Session.BeginWall("wall.back-left"),
                SurfaceSessionFailure.ActivePreviewMustFinish);

            Assert.That(fixture.Session.ActivePreview.TargetWallSurfaceId,
                Is.EqualTo("wall.back-right"));
            Assert.That(SnapshotJson(fixture.Session.ActivePreview.ProposedSnapshot),
                Is.EqualTo(beforeRejectedRetarget));
        }

        [Test]
        [Category("Recovery")]
        public void WallPreview_SelectingOriginalCombinationClearsHasChanges()
        {
            using var fixture = new Fixture();
            AssertSucceeded(fixture.Session.BeginWall("wall.back-left"));
            AssertSucceeded(fixture.Session.SelectStyle("wallpaper.cream-floral"));
            AssertSucceeded(fixture.Session.SelectStyle("wainscoting.sage-plain"));
            Assert.That(fixture.Session.ActivePreview.HasChanges, Is.True);

            AssertSucceeded(fixture.Session.SelectStyle("paint.cream"));
            AssertSucceeded(fixture.Session.SelectStyle("wains.raised"));

            Assert.That(fixture.Session.ActivePreview.HasChanges, Is.False);
        }

        [Test]
        [Category("Normal")]
        public void WallPreview_ConfirmIsAtomicAcrossBothLayers()
        {
            using var fixture = new Fixture();
            var confirmed = fixture.Layout;

            Assert.That(fixture.Session.BeginWall("wall.back-left").Succeeded, Is.True);
            Assert.That(fixture.Session.SelectStyle("wallpaper.cream-floral").Succeeded, Is.True);
            Assert.That(fixture.Session.SelectStyle("wainscoting.sage-plain").Succeeded, Is.True);
            Assert.That(confirmed.TryGetWall("wall.back-left", out var before), Is.True);
            Assert.That(before.BaseStyleId, Is.Not.EqualTo("wallpaper.cream-floral"));

            AssertSucceeded(fixture.Session.Confirm());

            Assert.That(confirmed.TryGetWall("wall.back-left", out var after), Is.True);
            Assert.That(after.BaseStyleId, Is.EqualTo("wallpaper.cream-floral"));
            Assert.That(after.WainscotingStyleId, Is.EqualTo("wainscoting.sage-plain"));
        }

        [Test]
        [Category("Recovery")]
        public void WallPreview_FailedConfirmPreservesPreviewAndConfirmedStateForRetryOrCancel()
        {
            using var fixture = new Fixture();
            AssertSucceeded(fixture.Session.BeginWall("wall.back-left"));
            AssertSucceeded(fixture.Session.SelectStyle("wallpaper.cream-floral"));
            AssertSucceeded(fixture.Session.SelectStyle("wainscoting.sage-plain"));

            var activeState = typeof(SurfaceDecorationSession).GetField(
                "activeState", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(fixture.Session);
            var proposedLayout = activeState.GetType().GetField(
                "ProposedLayout", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(proposedLayout, Is.Not.Null);
            var validProposedLayout = proposedLayout.GetValue(activeState);
            var invalidSnapshot = fixture.Session.ActivePreview.ProposedSnapshot;
            invalidSnapshot.RoomId = "room.foreign";
            proposedLayout.SetValue(activeState, RoomSurfaceLayout.FromSnapshot(invalidSnapshot));

            var previewBefore = fixture.Session.ActivePreview;
            var previewSnapshotBefore = SnapshotJson(previewBefore.ProposedSnapshot);
            var confirmedBefore = SnapshotJson(fixture.Layout);

            Assert.Throws<ArgumentException>(() => fixture.Session.Confirm());

            Assert.That(SnapshotJson(fixture.Layout), Is.EqualTo(confirmedBefore));
            Assert.That(fixture.Session.ActivePreview, Is.Not.Null);
            Assert.That(fixture.Session.ActivePreview.TargetWallSurfaceId,
                Is.EqualTo(previewBefore.TargetWallSurfaceId));
            Assert.That(SnapshotJson(fixture.Session.ActivePreview.ProposedSnapshot),
                Is.EqualTo(previewSnapshotBefore));
            Assert.That(fixture.Session.ActivePreview.HasChanges,
                Is.EqualTo(previewBefore.HasChanges));
            Assert.That(fixture.Session.ActivePreview.UsingWallBaseStyleId,
                Is.EqualTo(previewBefore.UsingWallBaseStyleId));
            Assert.That(fixture.Session.ActivePreview.PreviewWallBaseStyleId,
                Is.EqualTo(previewBefore.PreviewWallBaseStyleId));
            Assert.That(fixture.Session.ActivePreview.UsingWallWainscotingStyleId,
                Is.EqualTo(previewBefore.UsingWallWainscotingStyleId));
            Assert.That(fixture.Session.ActivePreview.PreviewWallWainscotingStyleId,
                Is.EqualTo(previewBefore.PreviewWallWainscotingStyleId));

            proposedLayout.SetValue(activeState, validProposedLayout);
            AssertSucceeded(fixture.Session.Confirm());
            Assert.That(fixture.Session.ActivePreview, Is.Null);
            Assert.That(fixture.Layout.TryGetWall("wall.back-left", out var committed), Is.True);
            Assert.That(committed.BaseStyleId, Is.EqualTo("wallpaper.cream-floral"));
            Assert.That(committed.WainscotingStyleId, Is.EqualTo("wainscoting.sage-plain"));
        }

        [Test]
        [Category("Normal")]
        public void AT041_WholeRoomPreview_ChangesAll64ProposedCellsButNotConfirmedLayout()
        {
            using var fixture = new Fixture();
            var confirmedBefore = SnapshotJson(fixture.Layout);
            AssertSucceeded(fixture.Session.BeginWholeRoomFloor());
            AssertSucceeded(fixture.Session.SelectStyle("floor.tile"));
            AssertSucceeded(fixture.Session.RotateFloor());

            var proposed = fixture.Session.ActivePreview.ProposedSnapshot;
            Assert.That(proposed.FloorTiles, Has.Count.EqualTo(64));
            Assert.That(proposed.FloorTiles.Select(tile => tile.StyleId),
                Is.All.EqualTo("floor.tile"));
            Assert.That(proposed.FloorTiles.Select(tile => tile.Rotation),
                Is.All.EqualTo(SurfaceRotation.Degrees90));
            Assert.That(SnapshotJson(fixture.Layout), Is.EqualTo(confirmedBefore));
        }

        [Test]
        [Category("Normal")]
        public void AT042_WholeRoomSemanticState_HasPreviewStyleButNeverUsingStyle()
        {
            using var fixture = new Fixture();
            AssertSucceeded(fixture.Session.BeginWholeRoomFloor());
            Assert.That(fixture.Session.ActivePreview.UsingStyleId, Is.Null);
            Assert.That(fixture.Session.ActivePreview.PreviewStyleId, Is.Null);

            AssertSucceeded(fixture.Session.SelectStyle("floor.stone"));

            Assert.That(fixture.Session.ActivePreview.UsingStyleId, Is.Null);
            Assert.That(fixture.Session.ActivePreview.PreviewStyleId,
                Is.EqualTo("floor.stone"));
        }

        [Test]
        [Category("Boundary")]
        public void AT043_SingleGridTap_UsesArmedStyleAndKeepsBothCellsInOnePreview()
        {
            using var fixture = new Fixture();
            var confirmedBefore = SnapshotJson(fixture.Layout);
            AssertSucceeded(fixture.Session.BeginSingleGridFloor(new GridPosition(0, 0)));
            AssertSucceeded(fixture.Session.SelectStyle("floor.tile"));
            AssertSucceeded(fixture.Session.SelectFloorGrid(new GridPosition(7, 7)));

            var proposed = fixture.Session.ActivePreview.ProposedSnapshot;
            Assert.That(Floor(proposed, 0, 0).StyleId, Is.EqualTo("floor.tile"));
            Assert.That(Floor(proposed, 7, 7).StyleId, Is.EqualTo("floor.tile"));
            Assert.That(fixture.Session.ActivePreview.SelectedFloorPosition,
                Is.EqualTo(new GridPosition(7, 7)));
            Assert.That(fixture.Session.ActivePreview.ArmedStyleId, Is.EqualTo("floor.tile"));
            Assert.That(SnapshotJson(fixture.Layout), Is.EqualTo(confirmedBefore));
        }

        [Test]
        [Category("Boundary")]
        public void AT043_SingleGridPreviewedPositions_AreDerivedInGridOrderAndUndoRestoresOnlyLastOperation()
        {
            // Catches a production break where preview feedback omits a changed Floor,
            // uses tap order instead of GridPosition order, or does not restore after Undo.
            using var fixture = new Fixture();
            var first = new GridPosition(0, 0);
            var second = new GridPosition(1, 0);
            var laterTap = new GridPosition(7, 7);

            AssertSucceeded(fixture.Session.BeginSingleGridFloor(first));
            Assert.That(fixture.Session.ActivePreview.SelectedFloorPosition, Is.EqualTo(first));
            CollectionAssert.IsEmpty(fixture.Session.ActivePreview.PreviewedFloorPositions,
                "Selecting a target before a Style must not create a preview check.");

            AssertSucceeded(fixture.Session.SelectStyle("floor.tile"));
            Assert.That(fixture.Session.ActivePreview.ArmedStyleId, Is.EqualTo("floor.tile"));
            AssertSucceeded(fixture.Session.SelectFloorGrid(laterTap));
            AssertSucceeded(fixture.Session.SelectFloorGrid(second));

            CollectionAssert.AreEqual(
                new[] { first, second, laterTap },
                fixture.Session.ActivePreview.PreviewedFloorPositions,
                "Preview checks must derive from changed Floor appearances in GridPosition order.");

            Assert.That(fixture.Session.UndoLast(), Is.True);
            CollectionAssert.AreEqual(
                new[] { first, laterTap },
                fixture.Session.ActivePreview.PreviewedFloorPositions,
                "Undo must remove only its last Floor operation from feedback state.");
        }

        [Test]
        [Category("Normal")]
        public void AT044_SingleGridSemanticState_UsesConfirmedTargetAndPreviewArmedStyle()
        {
            using var fixture = new Fixture();
            AssertSucceeded(fixture.Session.BeginSingleGridFloor(new GridPosition(0, 0)));

            Assert.That(fixture.Session.ActivePreview.UsingStyleId, Is.EqualTo("floor.wood"));
            Assert.That(fixture.Session.ActivePreview.PreviewStyleId, Is.Null);

            AssertSucceeded(fixture.Session.SelectStyle("floor.tile"));

            Assert.That(fixture.Session.ActivePreview.UsingStyleId, Is.EqualTo("floor.wood"));
            Assert.That(fixture.Session.ActivePreview.PreviewStyleId, Is.EqualTo("floor.tile"));
        }

        [Test]
        [Category("Boundary")]
        public void AT045_Rotate_ChangesCurrentAndFutureTapsButNotEarlierPreviewCells()
        {
            using var fixture = new Fixture();
            AssertSucceeded(fixture.Session.BeginSingleGridFloor(new GridPosition(0, 0)));
            AssertSucceeded(fixture.Session.SelectStyle("floor.tile"));
            AssertSucceeded(fixture.Session.SelectFloorGrid(new GridPosition(1, 0)));
            AssertSucceeded(fixture.Session.RotateFloor());
            AssertSucceeded(fixture.Session.SelectFloorGrid(new GridPosition(2, 0)));

            var proposed = fixture.Session.ActivePreview.ProposedSnapshot;
            Assert.That(Floor(proposed, 0, 0).Rotation, Is.EqualTo(SurfaceRotation.Degrees0));
            Assert.That(Floor(proposed, 1, 0).Rotation, Is.EqualTo(SurfaceRotation.Degrees90));
            Assert.That(Floor(proposed, 2, 0).Rotation, Is.EqualTo(SurfaceRotation.Degrees90));
            Assert.That(fixture.Session.ActivePreview.ArmedRotation,
                Is.EqualTo(SurfaceRotation.Degrees90));
        }

        [Test]
        [Category("Normal")]
        public void AT046_ArmedStyleAndRotationPersistUntilChangedAndEachTapCanUndo()
        {
            using var fixture = new Fixture();
            AssertSucceeded(fixture.Session.BeginSingleGridFloor(new GridPosition(0, 0)));
            AssertSucceeded(fixture.Session.SelectStyle("floor.tile"));
            AssertSucceeded(fixture.Session.RotateFloor());
            AssertSucceeded(fixture.Session.SelectFloorGrid(new GridPosition(1, 0)));
            var beforeSecondTap = SnapshotJson(fixture.Session.ActivePreview.ProposedSnapshot);
            AssertSucceeded(fixture.Session.SelectFloorGrid(new GridPosition(2, 0)));
            Assert.That(fixture.Session.UndoLast(), Is.True);
            Assert.That(SnapshotJson(fixture.Session.ActivePreview.ProposedSnapshot),
                Is.EqualTo(beforeSecondTap));

            AssertSucceeded(fixture.Session.SelectStyle("floor.stone"));
            AssertSucceeded(fixture.Session.SelectFloorGrid(new GridPosition(3, 0)));
            var proposed = fixture.Session.ActivePreview.ProposedSnapshot;
            Assert.That(Floor(proposed, 0, 0).StyleId, Is.EqualTo("floor.tile"));
            Assert.That(Floor(proposed, 1, 0).StyleId, Is.EqualTo("floor.stone"));
            Assert.That(Floor(proposed, 3, 0).StyleId, Is.EqualTo("floor.stone"));
            Assert.That(Floor(proposed, 3, 0).Rotation, Is.EqualTo(SurfaceRotation.Degrees90));
        }

        [Test]
        [Category("Invalid")]
        public void AT047_ActiveFloorPreview_RejectsScopeSwitchWithoutMutation()
        {
            using var fixture = new Fixture();
            AssertSucceeded(fixture.Session.BeginSingleGridFloor(new GridPosition(0, 0)));
            AssertSucceeded(fixture.Session.SelectStyle("floor.tile"));
            var previewBefore = SnapshotJson(fixture.Session.ActivePreview.ProposedSnapshot);
            var confirmedBefore = SnapshotJson(fixture.Layout);

            AssertFailure(
                fixture.Session.BeginWholeRoomFloor(),
                SurfaceSessionFailure.ActivePreviewMustFinish);

            Assert.That(fixture.Session.ActivePreview.Scope,
                Is.EqualTo(SurfaceEditScope.SingleGridFloor));
            Assert.That(SnapshotJson(fixture.Session.ActivePreview.ProposedSnapshot),
                Is.EqualTo(previewBefore));
            Assert.That(SnapshotJson(fixture.Layout), Is.EqualTo(confirmedBefore));
        }

        [Test]
        [Category("Recovery")]
        public void AT048_Undo_RestoresRotationThenApplyAllAsOneStepWithoutWritingConfirmed()
        {
            using var fixture = new Fixture();
            var confirmedBefore = SnapshotJson(fixture.Layout);
            AssertSucceeded(fixture.Session.BeginSingleGridFloor(new GridPosition(0, 0)));
            AssertSucceeded(fixture.Session.SelectStyle("floor.tile"));
            var beforeRotation = SnapshotJson(fixture.Session.ActivePreview.ProposedSnapshot);
            AssertSucceeded(fixture.Session.RotateFloor());
            var beforeApplyAll = SnapshotJson(fixture.Session.ActivePreview.ProposedSnapshot);
            AssertSucceeded(fixture.Session.ApplyAll());

            Assert.That(fixture.Session.UndoLast(), Is.True);
            Assert.That(SnapshotJson(fixture.Session.ActivePreview.ProposedSnapshot),
                Is.EqualTo(beforeApplyAll));
            Assert.That(fixture.Session.ActivePreview.ArmedRotation,
                Is.EqualTo(SurfaceRotation.Degrees90));
            Assert.That(fixture.Session.UndoLast(), Is.True);
            Assert.That(SnapshotJson(fixture.Session.ActivePreview.ProposedSnapshot),
                Is.EqualTo(beforeRotation));
            Assert.That(fixture.Session.ActivePreview.ArmedRotation,
                Is.EqualTo(SurfaceRotation.Degrees0));
            Assert.That(fixture.Session.UndoLast(), Is.True);
            Assert.That(fixture.Session.ActivePreview.ArmedStyleId, Is.Null);
            Assert.That(fixture.Session.ActivePreview.CanUndo, Is.False);
            Assert.That(fixture.Session.UndoLast(), Is.False);
            Assert.That(SnapshotJson(fixture.Layout), Is.EqualTo(confirmedBefore));
        }

        [Test]
        [Category("Recovery")]
        public void AT049_Cancel_DiscardsAllChangesAndIsIdempotent()
        {
            using var fixture = new Fixture();
            var before = SnapshotJson(fixture.Layout);
            AssertSucceeded(fixture.Session.BeginWholeRoomFloor());
            AssertSucceeded(fixture.Session.SelectStyle("floor.stone"));
            AssertSucceeded(fixture.Session.RotateFloor());

            fixture.Session.Cancel();
            fixture.Session.Cancel();

            Assert.That(fixture.Session.ActivePreview, Is.Null);
            Assert.That(SnapshotJson(fixture.Layout), Is.EqualTo(before));
        }

        [Test]
        [Category("Recovery")]
        public void AT050_Confirm_CommitsOnceAndLaterConfirmOrUndoCannotMutateAgain()
        {
            using var fixture = new Fixture();
            AssertSucceeded(fixture.Session.BeginWall("wall.back-left"));
            AssertSucceeded(fixture.Session.SelectStyle("paint.sage"));

            AssertSucceeded(fixture.Session.Confirm());
            var afterFirstConfirm = SnapshotJson(fixture.Layout);
            Assert.That(Wall(fixture.Layout.CaptureSnapshot(), "wall.back-left").BaseStyleId,
                Is.EqualTo("paint.sage"));

            AssertFailure(fixture.Session.Confirm(), SurfaceSessionFailure.NoActivePreview);
            Assert.That(fixture.Session.UndoLast(), Is.False);
            Assert.That(SnapshotJson(fixture.Layout), Is.EqualTo(afterFirstConfirm));
        }

        [Test]
        [Category("Invalid")]
        public void AT051_SecondBeginOnSameSession_IsRejectedWithoutLosingFirstPreview()
        {
            using var fixture = new Fixture();
            AssertSucceeded(fixture.Session.BeginWholeRoomFloor());
            AssertSucceeded(fixture.Session.SelectStyle("floor.tile"));
            var before = SnapshotJson(fixture.Session.ActivePreview.ProposedSnapshot);

            AssertFailure(
                fixture.Session.BeginSingleGridFloor(new GridPosition(4, 4)),
                SurfaceSessionFailure.ActivePreviewMustFinish);
            AssertFailure(
                fixture.Session.BeginWall("wall.back-left"),
                SurfaceSessionFailure.ActivePreviewMustFinish);

            Assert.That(fixture.Session.ActivePreview.Scope,
                Is.EqualTo(SurfaceEditScope.WholeRoomFloor));
            Assert.That(SnapshotJson(fixture.Session.ActivePreview.ProposedSnapshot),
                Is.EqualTo(before));
        }

        [Test]
        [Category("Regression")]
        public void AT052_SurfaceOperations_OnlyChangeAppearanceAndKeepEveryGridPositionStable()
        {
            using var fixture = new Fixture();
            var original = fixture.Layout.CaptureSnapshot();
            var originalJson = SnapshotJson(original);
            var originalPositions = original.FloorTiles
                .Select(tile => (tile.X, tile.Y)).ToArray();
            var originalWalls = original.Walls
                .Select(wall => wall.SurfaceId).ToArray();

            AssertSucceeded(fixture.Session.BeginSingleGridFloor(new GridPosition(0, 0)));
            AssertSucceeded(fixture.Session.SelectStyle("floor.tile"));
            AssertSucceeded(fixture.Session.RotateFloor());
            AssertSucceeded(fixture.Session.ApplyAll());
            Assert.That(SnapshotJson(fixture.Layout), Is.EqualTo(originalJson));
            Assert.That(fixture.Session.ActivePreview.ProposedSnapshot.FloorTiles
                .Select(tile => (tile.X, tile.Y)), Is.EqualTo(originalPositions));
            fixture.Session.Cancel();
            Assert.That(SnapshotJson(fixture.Layout), Is.EqualTo(originalJson));

            AssertSucceeded(fixture.Session.BeginSingleGridFloor(new GridPosition(0, 0)));
            AssertSucceeded(fixture.Session.SelectStyle("floor.tile"));
            AssertSucceeded(fixture.Session.RotateFloor());
            AssertSucceeded(fixture.Session.ApplyAll());
            AssertSucceeded(fixture.Session.Confirm());

            var confirmed = fixture.Layout.CaptureSnapshot();
            Assert.That(confirmed.FloorTiles.Select(tile => (tile.X, tile.Y)),
                Is.EqualTo(originalPositions));
            Assert.That(confirmed.Walls.Select(wall => wall.SurfaceId), Is.EqualTo(originalWalls));
            Assert.That(confirmed.FloorTiles.Select(tile => tile.StyleId),
                Is.All.EqualTo("floor.tile"));
            Assert.That(confirmed.FloorTiles.Select(tile => tile.Rotation),
                Is.All.EqualTo(SurfaceRotation.Degrees90));

            var forbiddenTypes = new[]
            {
                typeof(CafeLayout),
                typeof(WallMountedLayout),
                typeof(Collider),
                typeof(UnityEngine.AI.NavMeshData)
            };
            var runtimeFields = new[]
                {
                    typeof(SurfaceDecorationSession),
                    typeof(SurfacePreviewTransaction)
                }
                .SelectMany(type => type.GetFields(
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic));
            Assert.That(runtimeFields.Select(field => field.FieldType)
                .Any(type => forbiddenTypes.Contains(type)), Is.False);
        }

        [Test]
        [Category("Extra")]
        public void InvalidTargets_FailWithoutCreatingOrMutatingAPreview()
        {
            using var fixture = new Fixture();
            var confirmedBefore = SnapshotJson(fixture.Layout);

            AssertFailure(
                fixture.Session.BeginWall("wall.missing"),
                SurfaceSessionFailure.UnknownTarget);
            AssertFailure(
                fixture.Session.BeginSingleGridFloor(new GridPosition(-1, 0)),
                SurfaceSessionFailure.UnknownTarget);
            AssertSucceeded(fixture.Session.BeginWall("wall.back-left"));
            AssertFailure(
                fixture.Session.SelectStyle("floor.tile"),
                SurfaceSessionFailure.WrongStyleKind);
            fixture.Session.Cancel();

            Assert.That(fixture.Session.ActivePreview, Is.Null);
            Assert.That(SnapshotJson(fixture.Layout), Is.EqualTo(confirmedBefore));
        }

        [Test]
        [Category("Extra")]
        public void Constructor_CopiesStyleLookupAndRejectsNullOrDuplicateEntries()
        {
            using var fixture = new Fixture();
            var source = fixture.Styles.ToList();
            var session = new SurfaceDecorationSession(fixture.Layout, source);
            source.Clear();
            AssertSucceeded(session.BeginWholeRoomFloor());
            AssertSucceeded(session.SelectStyle("floor.tile"));

            Assert.Throws<ArgumentNullException>(() =>
                new SurfaceDecorationSession(fixture.Layout, null));
            Assert.Throws<ArgumentException>(() =>
                new SurfaceDecorationSession(fixture.Layout,
                    fixture.Styles.Concat(new SurfaceStyleDefinitionAsset[] { null })));
            fixture.CreateTrackedStyle(
                "floor.tile",
                SurfaceStyleKind.Floor);
            Assert.Throws<ArgumentException>(() =>
                new SurfaceDecorationSession(fixture.Layout,
                    fixture.Styles));
        }

        [Test]
        [Category("Extra")]
        public void ProposedSnapshot_IsADefensiveCopy()
        {
            using var fixture = new Fixture();
            AssertSucceeded(fixture.Session.BeginWholeRoomFloor());
            AssertSucceeded(fixture.Session.SelectStyle("floor.tile"));
            var expected = SnapshotJson(fixture.Session.ActivePreview.ProposedSnapshot);

            var published = fixture.Session.ActivePreview.ProposedSnapshot;
            published.RoomId = "room.mutated";
            published.Walls.Clear();
            published.FloorTiles[0].StyleId = "floor.mutated";
            published.FloorTiles.Clear();

            Assert.That(SnapshotJson(fixture.Session.ActivePreview.ProposedSnapshot),
                Is.EqualTo(expected));
        }

        [Test]
        [Category("Extra")]
        public void WallSemanticState_ExposesSeparateBaseAndNoneOverlayIds()
        {
            using var fixture = new Fixture();
            AssertSucceeded(fixture.Session.BeginWall("wall.back-right"));

            Assert.That(fixture.Session.ActivePreview.UsingWallBaseStyleId,
                Is.EqualTo("wallpaper.floral"));
            Assert.That(fixture.Session.ActivePreview.PreviewWallBaseStyleId,
                Is.Null);
            Assert.That(fixture.Session.ActivePreview.UsingWallWainscotingStyleId,
                Is.EqualTo("wains.none"));
            Assert.That(fixture.Session.ActivePreview.PreviewWallWainscotingStyleId,
                Is.Null);

            AssertSucceeded(fixture.Session.SelectStyle("wains.panel"));

            Assert.That(fixture.Session.ActivePreview.UsingWallWainscotingStyleId,
                Is.EqualTo("wains.none"));
            Assert.That(fixture.Session.ActivePreview.PreviewWallWainscotingStyleId,
                Is.EqualTo("wains.panel"));
        }

        [Test]
        [Category("Extra")]
        public void ActivePreview_ReturnsAnImmutablePointInTimeView()
        {
            using var fixture = new Fixture();
            AssertSucceeded(fixture.Session.BeginWholeRoomFloor());
            var retainedView = fixture.Session.ActivePreview;
            var retainedSnapshot = SnapshotJson(retainedView.ProposedSnapshot);

            AssertSucceeded(fixture.Session.SelectStyle("floor.tile"));

            Assert.That(retainedView.ArmedStyleId, Is.Null);
            Assert.That(retainedView.PreviewStyleId, Is.Null);
            Assert.That(retainedView.CanUndo, Is.False);
            Assert.That(SnapshotJson(retainedView.ProposedSnapshot),
                Is.EqualTo(retainedSnapshot));
            Assert.That(fixture.Session.ActivePreview.ArmedStyleId,
                Is.EqualTo("floor.tile"));
            Assert.That(fixture.Session.ActivePreview, Is.Not.SameAs(retainedView));
        }

        [Test]
        [Category("Extra")]
        public void SurfacePreviewTransaction_HasNoPublicOrInternalMutationSurface()
        {
            var type = typeof(SurfacePreviewTransaction);
            var writableProperties = type.GetProperties(
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic |
                    BindingFlags.DeclaredOnly)
                .Where(property => property.SetMethod != null &&
                    !property.SetMethod.IsPrivate)
                .Select(property => property.Name)
                .ToArray();
            var callableMutators = type.GetMethods(
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic |
                    BindingFlags.DeclaredOnly)
                .Where(method => !method.IsSpecialName && !method.IsPrivate)
                .Select(method => method.Name)
                .ToArray();
            var mutableLayoutMembers = type.GetProperties(
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic |
                    BindingFlags.DeclaredOnly)
                .Where(property => property.PropertyType == typeof(RoomSurfaceLayout))
                .Select(property => property.Name)
                .ToArray();

            Assert.That(writableProperties, Is.Empty);
            Assert.That(callableMutators, Is.Empty);
            Assert.That(mutableLayoutMembers, Is.Empty);
        }

        [Test]
        [Category("Extra")]
        public void Confirm_UsesAggregateStateSwapInsteadOfMutatingPublishedDictionaries()
        {
            using var fixture = new Fixture();
            var retainedWalls = fixture.Layout.Walls;
            var retainedFloors = fixture.Layout.FloorTiles;
            AssertSucceeded(fixture.Session.BeginWholeRoomFloor());
            AssertSucceeded(fixture.Session.SelectStyle("floor.stone"));
            AssertSucceeded(fixture.Session.RotateFloor());

            AssertSucceeded(fixture.Session.Confirm());

            Assert.That(fixture.Layout.FloorTiles.Values.Select(floor => floor.StyleId),
                Is.All.EqualTo("floor.stone"));
            Assert.That(fixture.Layout.FloorTiles.Values.Select(floor => floor.Rotation),
                Is.All.EqualTo(SurfaceRotation.Degrees90));
            Assert.That(retainedFloors.Values.Select(floor => floor.StyleId),
                Is.All.EqualTo("floor.wood"));
            Assert.That(retainedFloors.Values.Select(floor => floor.Rotation),
                Is.All.EqualTo(SurfaceRotation.Degrees0));
            Assert.That(retainedWalls.Values.Select(wall => wall.BaseStyleId),
                Is.EquivalentTo(new[] { "paint.cream", "wallpaper.floral" }));
        }

        [Test]
        [Category("Extra")]
        public void Constructor_RejectsMalformedSurfaceDefinitionsAndRequiresExactlyOneNone()
        {
            using var fixture = new Fixture();
            var paint = fixture.Style("paint.cream");
            var none = fixture.Style("wains.none");
            var panel = fixture.Style("wains.panel");

            fixture.SetStyle(paint, clearStyleId: true);
            Assert.Throws<ArgumentException>(() => fixture.CreateSession());
            fixture.SetStyle(paint, styleId: "paint.cream");

            fixture.SetStyle(paint, styleId: "Paint.Bad");
            Assert.Throws<ArgumentException>(() => fixture.CreateSession());
            fixture.SetStyle(paint, styleId: "paint.cream");

            fixture.SetStyle(paint, clearMaterial: true);
            Assert.Throws<ArgumentException>(() => fixture.CreateSession());
            fixture.SetStyle(paint, material: fixture.Material);

            fixture.SetStyle(paint, clearThumbnail: true);
            Assert.Throws<ArgumentException>(() => fixture.CreateSession());
            fixture.SetStyle(paint, thumbnail: fixture.Thumbnail);

            fixture.SetStyle(paint, kindValue: 999);
            Assert.Throws<ArgumentException>(() => fixture.CreateSession());
            fixture.SetStyle(paint, kindValue: (int)SurfaceStyleKind.Paint);

            fixture.SetStyle(none, kindValue: (int)SurfaceStyleKind.Paint);
            Assert.Throws<ArgumentException>(() => fixture.CreateSession());
            fixture.SetStyle(none, kindValue: (int)SurfaceStyleKind.Wainscoting);

            fixture.SetStyle(none, material: fixture.Material);
            Assert.Throws<ArgumentException>(() => fixture.CreateSession());
            fixture.SetStyle(none, clearMaterial: true);

            fixture.SetStyle(none, isNone: false, material: fixture.Material);
            Assert.Throws<ArgumentException>(() => fixture.CreateSession());
            fixture.SetStyle(none, isNone: true, clearMaterial: true);

            fixture.SetStyle(panel, isNone: true, clearMaterial: true);
            Assert.Throws<ArgumentException>(() => fixture.CreateSession());
        }

        [Test]
        [Category("Extra")]
        public void Constructor_FreezesBindingsAgainstLaterSourceAssetMutation()
        {
            using var fixture = new Fixture();
            var sourceFloor = fixture.Style("floor.tile");
            var session = fixture.CreateSession();

            fixture.SetStyle(
                sourceFloor,
                styleId: "paint.mutated",
                kindValue: (int)SurfaceStyleKind.Paint,
                isNone: true,
                clearMaterial: true,
                clearThumbnail: true);

            AssertSucceeded(session.BeginWholeRoomFloor());
            AssertSucceeded(session.SelectStyle("floor.tile"));
            AssertFailure(
                session.SelectStyle("paint.mutated"),
                SurfaceSessionFailure.UnknownStyle);
        }

        private static WallAppearanceSnapshotEntry Wall(
            RoomSurfaceSnapshot snapshot,
            string surfaceId)
        {
            return snapshot.Walls.Single(wall => wall.SurfaceId == surfaceId);
        }

        private static FloorTileAppearanceSnapshotEntry Floor(
            RoomSurfaceSnapshot snapshot,
            int x,
            int y)
        {
            return snapshot.FloorTiles.Single(tile => tile.X == x && tile.Y == y);
        }

        private static string SnapshotJson(RoomSurfaceLayout layout)
        {
            return SnapshotJson(layout.CaptureSnapshot());
        }

        private static string SnapshotJson(RoomSurfaceSnapshot snapshot)
        {
            return JsonUtility.ToJson(snapshot);
        }

        private static void AssertSucceeded(SurfaceSessionResult result)
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.FailureReason, Is.EqualTo(SurfaceSessionFailure.None));
        }

        private static void AssertFailure(
            SurfaceSessionResult result,
            SurfaceSessionFailure expectedFailure)
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailureReason, Is.EqualTo(expectedFailure));
        }

        private sealed class Fixture : IDisposable
        {
            private readonly List<SurfaceStyleDefinitionAsset> styles;
            private readonly Texture2D texture;

            public RoomSurfaceLayout Layout { get; }
            public SurfaceDecorationSession Session { get; }
            public IReadOnlyList<SurfaceStyleDefinitionAsset> Styles => styles;
            public Material Material { get; }
            public Sprite Thumbnail { get; }

            public Fixture()
            {
                Layout = CreateLayout("room.main");
                var shader = Shader.Find("Universal Render Pipeline/Lit") ??
                    Shader.Find("Standard");
                Material = new Material(shader) { name = "M_SurfaceSessionFixture" };
                texture = new Texture2D(1, 1) { name = "T_SurfaceSessionFixture" };
                Thumbnail = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, 1f, 1f),
                    Vector2.zero);
                styles = new List<SurfaceStyleDefinitionAsset>
                {
                    CreateStyle("paint.cream", SurfaceStyleKind.Paint, Material, Thumbnail),
                    CreateStyle("paint.sage", SurfaceStyleKind.Paint, Material, Thumbnail),
                    CreateStyle("wallpaper.cream-floral", SurfaceStyleKind.Wallpaper, Material, Thumbnail),
                    CreateStyle("wallpaper.floral", SurfaceStyleKind.Wallpaper, Material, Thumbnail),
                    CreateStyle("wallpaper.sprig", SurfaceStyleKind.Wallpaper, Material, Thumbnail),
                    CreateStyle("wains.raised", SurfaceStyleKind.Wainscoting, Material, Thumbnail),
                    CreateStyle("wains.panel", SurfaceStyleKind.Wainscoting, Material, Thumbnail),
                    CreateStyle("wainscoting.sage-plain", SurfaceStyleKind.Wainscoting, Material, Thumbnail),
                    CreateStyle(
                        "wains.none",
                        SurfaceStyleKind.Wainscoting,
                        null,
                        Thumbnail,
                        isNone: true),
                    CreateStyle("floor.wood", SurfaceStyleKind.Floor, Material, Thumbnail),
                    CreateStyle("floor.tile", SurfaceStyleKind.Floor, Material, Thumbnail),
                    CreateStyle("floor.stone", SurfaceStyleKind.Floor, Material, Thumbnail)
                };
                Session = new SurfaceDecorationSession(Layout, styles);
            }

            public void Dispose()
            {
                foreach (var style in styles)
                {
                    UnityEngine.Object.DestroyImmediate(style);
                }

                UnityEngine.Object.DestroyImmediate(Thumbnail);
                UnityEngine.Object.DestroyImmediate(texture);
                UnityEngine.Object.DestroyImmediate(Material);
            }

            public SurfaceDecorationSession CreateSession()
            {
                return new SurfaceDecorationSession(Layout, styles);
            }

            public SurfaceStyleDefinitionAsset Style(string styleId)
            {
                return styles.Single(style => style.StyleId == styleId);
            }

            public SurfaceStyleDefinitionAsset CreateTrackedStyle(
                string styleId,
                SurfaceStyleKind kind)
            {
                var style = CreateStyle(styleId, kind, Material, Thumbnail);
                styles.Add(style);
                return style;
            }

            public void SetStyle(
                SurfaceStyleDefinitionAsset style,
                string styleId = null,
                bool clearStyleId = false,
                int? kindValue = null,
                bool? isNone = null,
                Material material = null,
                bool clearMaterial = false,
                Sprite thumbnail = null,
                bool clearThumbnail = false)
            {
                var serialized = new SerializedObject(style);
                if (styleId != null || clearStyleId)
                {
                    serialized.FindProperty("styleId").stringValue =
                        clearStyleId ? null : styleId;
                }

                if (kindValue.HasValue)
                {
                    serialized.FindProperty("kind").intValue = kindValue.Value;
                }

                if (isNone.HasValue)
                {
                    serialized.FindProperty("isNoneOption").boolValue = isNone.Value;
                }

                if (material != null || clearMaterial)
                {
                    serialized.FindProperty("material").objectReferenceValue =
                        clearMaterial ? null : material;
                }

                if (thumbnail != null || clearThumbnail)
                {
                    serialized.FindProperty("thumbnail").objectReferenceValue =
                        clearThumbnail ? null : thumbnail;
                }

                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            public static RoomSurfaceLayout CreateLayout(string roomId)
            {
                var walls = new[]
                {
                    new WallAppearance(
                        "wall.back-left",
                        "paint.cream",
                        "wains.raised"),
                    new WallAppearance(
                        "wall.back-right",
                        "wallpaper.floral",
                        null)
                };
                var floors = new List<FloorTileAppearance>();
                for (var x = 0; x < 8; x++)
                {
                    for (var y = 0; y < 8; y++)
                    {
                        floors.Add(new FloorTileAppearance(
                            new GridPosition(x, y),
                            "floor.wood",
                            SurfaceRotation.Degrees0));
                    }
                }

                return new RoomSurfaceLayout(roomId, walls, floors);
            }

            public static SurfaceStyleDefinitionAsset CreateStyle(
                string styleId,
                SurfaceStyleKind kind,
                Material material,
                Sprite thumbnail,
                bool isNone = false)
            {
                var style = ScriptableObject.CreateInstance<SurfaceStyleDefinitionAsset>();
                var serialized = new SerializedObject(style);
                serialized.FindProperty("styleId").stringValue = styleId;
                serialized.FindProperty("kind").enumValueIndex = (int)kind;
                serialized.FindProperty("material").objectReferenceValue = material;
                serialized.FindProperty("thumbnail").objectReferenceValue = thumbnail;
                serialized.FindProperty("isNoneOption").boolValue = isNone;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                return style;
            }
        }
    }
}

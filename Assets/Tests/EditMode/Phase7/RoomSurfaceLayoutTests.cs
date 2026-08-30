using System;
using System.Collections.Generic;
using System.Linq;
using AnimalCafe.Layout;
using NUnit.Framework;
using UnityEngine;

namespace AnimalCafe.Tests.Phase7
{
    public sealed class RoomSurfaceLayoutTests
    {
        [Test]
        public void Constructor_TwoWallsAndEightByEightFloorTilesExposeStableRoomSurfaceState()
        {
            var layout = CreateLayout();

            Assert.That(layout.RoomId, Is.EqualTo("room.main"));
            Assert.That(layout.Walls, Has.Count.EqualTo(2));
            Assert.That(layout.FloorTiles, Has.Count.EqualTo(64));
            Assert.That(layout.TryGetWall("wall.back-left", out var leftWall), Is.True);
            Assert.That(leftWall.BaseStyleId, Is.EqualTo("paint.cream"));
            Assert.That(layout.TryGetFloor(new GridPosition(7, 7), out var finalTile), Is.True);
            Assert.That(finalTile.StyleId, Is.EqualTo("floor.wood.warm"));
        }

        [Test]
        public void AppearanceValueTypes_RejectMalformedStableIdsButAllowNullWainscoting()
        {
            var wall = new WallAppearance("wall.back-left", "paint.cream", null);

            Assert.That(wall.WainscotingStyleId, Is.Null);
            Assert.Throws<ArgumentException>(() =>
                new WallAppearance("Wall Back", "paint.cream", null));
            Assert.Throws<ArgumentException>(() =>
                new WallAppearance("wall.back-left", "Paint Cream", null));
            Assert.Throws<ArgumentException>(() =>
                new WallAppearance("wall.back-left", "paint.cream", "Wainscot White"));
            Assert.Throws<ArgumentException>(() =>
                new FloorTileAppearance(
                    new GridPosition(0, 0),
                    "Floor Wood",
                    SurfaceRotation.Degrees0));
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase("Room Main")]
        public void Constructor_RejectsMalformedRoomStableIds(string roomId)
        {
            Assert.That(
                () => new RoomSurfaceLayout(roomId, CreateWalls(), CreateFloorTiles()),
                Throws.InstanceOf<ArgumentException>());
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("Room.Main")]
        [TestCase("room/main")]
        public void Constructor_RejectsIllegalRoomIdMatrix(string roomId)
        {
            Assert.That(
                () => new RoomSurfaceLayout(roomId, CreateWalls(), CreateFloorTiles()),
                Throws.InstanceOf<ArgumentException>());
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("Wall.Back")]
        [TestCase("wall/back")]
        public void ReplaceWall_RejectsIllegalSurfaceIdMatrixWithoutMutation(string surfaceId)
        {
            var layout = CreateLayout();
            var before = SnapshotLayout(layout);

            Assert.That(
                () => layout.ReplaceWall(new WallAppearance(surfaceId, "paint.sage", null)),
                Throws.InstanceOf<ArgumentException>());

            Assert.That(SnapshotLayout(layout), Is.EqualTo(before));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("Paint.Sage")]
        [TestCase("paint/sage")]
        public void ReplaceWall_RejectsIllegalBaseStyleIdMatrixWithoutMutation(string baseStyleId)
        {
            var layout = CreateLayout();
            var before = SnapshotLayout(layout);

            Assert.That(
                () => layout.ReplaceWall(new WallAppearance(
                    "wall.back-left", baseStyleId, null)),
                Throws.InstanceOf<ArgumentException>());

            Assert.That(SnapshotLayout(layout), Is.EqualTo(before));
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase("Wainscot.White")]
        [TestCase("wainscot/white")]
        public void ReplaceWall_RejectsIllegalNonNullWainscotingIdMatrixWithoutMutation(
            string wainscotingStyleId)
        {
            var layout = CreateLayout();
            var before = SnapshotLayout(layout);

            Assert.That(
                () => layout.ReplaceWall(new WallAppearance(
                    "wall.back-left", "paint.sage", wainscotingStyleId)),
                Throws.InstanceOf<ArgumentException>());

            Assert.That(SnapshotLayout(layout), Is.EqualTo(before));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("Floor.Wood")]
        [TestCase("floor/wood")]
        public void ReplaceFloor_RejectsIllegalStyleIdMatrixWithoutMutation(string styleId)
        {
            var layout = CreateLayout();
            var before = SnapshotLayout(layout);

            Assert.That(
                () => layout.ReplaceFloor(new FloorTileAppearance(
                    new GridPosition(0, 0), styleId, SurfaceRotation.Degrees0)),
                Throws.InstanceOf<ArgumentException>());

            Assert.That(SnapshotLayout(layout), Is.EqualTo(before));
        }

        [Test]
        public void FloorTileAppearance_PreservesEachTileRotation()
        {
            var tile = new FloorTileAppearance(
                new GridPosition(3, 5),
                "floor.tile.light",
                SurfaceRotation.Degrees270);

            Assert.That(tile.Position, Is.EqualTo(new GridPosition(3, 5)));
            Assert.That(tile.StyleId, Is.EqualTo("floor.tile.light"));
            Assert.That(tile.Rotation, Is.EqualTo(SurfaceRotation.Degrees270));
        }

        [Test]
        public void Constructor_RejectsMissingWallsAndDuplicateWallSurfaceIds()
        {
            Assert.Throws<ArgumentException>(() =>
                new RoomSurfaceLayout("room.main", Array.Empty<WallAppearance>(), CreateFloorTiles()));
            Assert.Throws<ArgumentException>(() =>
                new RoomSurfaceLayout(
                    "room.main",
                    new[]
                    {
                        new WallAppearance("wall.back-left", "paint.cream", null),
                        new WallAppearance("wall.back-left", "paint.sage", null)
                    },
                    CreateFloorTiles()));

            Assert.Throws<ArgumentException>(() =>
                new RoomSurfaceLayout(
                    "room.main",
                    new[]
                    {
                        new WallAppearance("wall.back-left", "paint.cream", null),
                        new WallAppearance("wall.back-right", "paint.sage", null),
                        new WallAppearance("wall.front", "wallpaper.sage.sprig", null)
                    },
                    CreateFloorTiles()));
        }

        [Test]
        public void Constructor_RejectsDuplicateOrOutsideFloorPositions()
        {
            var duplicateTiles = CreateFloorTiles().ToList();
            duplicateTiles[63] = new FloorTileAppearance(
                new GridPosition(0, 0),
                "floor.tile.light",
                SurfaceRotation.Degrees90);

            Assert.Throws<ArgumentException>(() =>
                new RoomSurfaceLayout("room.main", CreateWalls(), duplicateTiles));

            var outsideTiles = CreateFloorTiles().ToList();
            outsideTiles[63] = new FloorTileAppearance(
                new GridPosition(8, 0),
                "floor.tile.light",
                SurfaceRotation.Degrees0);

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new RoomSurfaceLayout("room.main", CreateWalls(), outsideTiles));

            var missingTiles = CreateFloorTiles().Take(63).ToArray();
            Assert.Throws<ArgumentException>(() =>
                new RoomSurfaceLayout("room.main", CreateWalls(), missingTiles));

            foreach (var outsidePosition in new[]
            {
                new GridPosition(-1, 0),
                new GridPosition(8, 7)
            })
            {
                var tiles = CreateFloorTiles().ToList();
                tiles[63] = new FloorTileAppearance(
                    outsidePosition,
                    "floor.tile.light",
                    SurfaceRotation.Degrees0);

                Assert.Throws<ArgumentOutOfRangeException>(() =>
                    new RoomSurfaceLayout("room.main", CreateWalls(), tiles));
            }
        }

        [Test]
        public void Constructor_RejectedDuplicateOrMissingFloorSourcesRemainUnchanged()
        {
            var duplicateTiles = CreateFloorTiles().ToList();
            duplicateTiles[63] = new FloorTileAppearance(
                new GridPosition(0, 0),
                "floor.tile.light",
                SurfaceRotation.Degrees90);
            AssertConstructorRejectsWithoutMutatingSources(CreateWalls().ToList(), duplicateTiles);

            var missingTiles = CreateFloorTiles().Take(63).ToList();
            AssertConstructorRejectsWithoutMutatingSources(CreateWalls().ToList(), missingTiles);
        }

        [TestCase(-1, 0)]
        [TestCase(8, 7)]
        public void Constructor_RejectedOutsideFloorSourcesRemainUnchanged(int x, int y)
        {
            var tiles = CreateFloorTiles().ToList();
            tiles[63] = new FloorTileAppearance(
                new GridPosition(x, y),
                "floor.tile.light",
                SurfaceRotation.Degrees0);

            AssertConstructorRejectsWithoutMutatingSources(CreateWalls().ToList(), tiles);
        }

        [Test]
        public void WallAppearances_KeepNullOnlyForNoWainscotingAndNonNullOverlaySeparately()
        {
            var layout = CreateLayout();

            Assert.That(layout.Walls["wall.back-left"].BaseStyleId, Is.EqualTo("paint.cream"));
            Assert.That(layout.Walls["wall.back-left"].WainscotingStyleId, Is.Null);
            Assert.That(layout.Walls["wall.back-right"].BaseStyleId,
                Is.EqualTo("wallpaper.sage.sprig"));
            Assert.That(layout.Walls["wall.back-right"].WainscotingStyleId,
                Is.EqualTo("wainscot.warm.white"));
        }

        [Test]
        public void FloorTiles_PreserveAllFourQuarterTurnRotationsAndCompleteCycleReturnsToDegrees0()
        {
            var sourceTiles = CreateFloorTilesWithMixedAppearances().ToArray();
            var layout = new RoomSurfaceLayout(
                "room.main",
                CreateWalls(),
                sourceTiles);
            var rotations = new[]
            {
                SurfaceRotation.Degrees0,
                SurfaceRotation.Degrees90,
                SurfaceRotation.Degrees180,
                SurfaceRotation.Degrees270
            };

            foreach (var sourceTile in sourceTiles)
            {
                Assert.That(
                    layout.TryGetFloor(sourceTile.Position, out var tile),
                    Is.True);
                Assert.That(tile.StyleId, Is.EqualTo(sourceTile.StyleId));
                Assert.That(tile.Rotation, Is.EqualTo(sourceTile.Rotation));
            }

            Assert.That(
                (SurfaceRotation)(((int)SurfaceRotation.Degrees0 + rotations.Length) %
                    rotations.Length),
                Is.EqualTo(SurfaceRotation.Degrees0));
        }

        [Test]
        public void Collections_AreReadOnlyDefensiveViews()
        {
            var layout = CreateLayout();
            var walls = layout.Walls as IDictionary<string, WallAppearance>;
            var floorTiles = layout.FloorTiles as IDictionary<GridPosition, FloorTileAppearance>;

            Assert.That(walls, Is.Not.Null);
            Assert.That(floorTiles, Is.Not.Null);
            Assert.Throws<NotSupportedException>(() => walls.Add(
                "wall.front", new WallAppearance("wall.front", "paint.sage", null)));
            Assert.Throws<NotSupportedException>(() => floorTiles.Add(
                new GridPosition(8, 0),
                new FloorTileAppearance(
                    new GridPosition(8, 0),
                    "floor.tile.light",
                    SurfaceRotation.Degrees0)));
            Assert.That(layout.Walls, Has.Count.EqualTo(2));
            Assert.That(layout.FloorTiles, Has.Count.EqualTo(64));
        }

        [Test]
        public void Collections_DefensivelyCopyMutableConstructionSources()
        {
            var walls = CreateWalls().ToList();
            var floorTiles = CreateFloorTiles().ToList();
            var layout = new RoomSurfaceLayout("room.main", walls, floorTiles);

            walls[0] = new WallAppearance("wall.back-left", "paint.sage", null);
            floorTiles[0] = new FloorTileAppearance(
                new GridPosition(0, 0),
                "floor.stone.dark",
                SurfaceRotation.Degrees180);

            Assert.That(layout.Walls["wall.back-left"].BaseStyleId, Is.EqualTo("paint.cream"));
            Assert.That(layout.FloorTiles[new GridPosition(0, 0)].StyleId,
                Is.EqualTo("floor.wood.warm"));
        }

        [Test]
        public void ReplaceWall_ChangesOnlyTheTargetWallAndPreservesAllFloorTiles()
        {
            var layout = CreateLayout();
            var beforeWalls = SnapshotWalls(layout);
            var beforeFloors = SnapshotFloorTiles(layout);

            layout.ReplaceWall(new WallAppearance(
                "wall.back-left",
                "wallpaper.cream.floral",
                "wainscot.sage.plain"));

            Assert.That(layout.Walls["wall.back-left"].BaseStyleId,
                Is.EqualTo("wallpaper.cream.floral"));
            Assert.That(layout.Walls["wall.back-left"].WainscotingStyleId,
                Is.EqualTo("wainscot.sage.plain"));
            Assert.That(layout.Walls["wall.back-right"].BaseStyleId,
                Is.EqualTo("wallpaper.sage.sprig"));
            Assert.That(SnapshotFloorTiles(layout), Is.EqualTo(beforeFloors));
            Assert.That(SnapshotWalls(layout)[0], Is.Not.EqualTo(beforeWalls[0]));
            Assert.That(SnapshotWalls(layout)[1], Is.EqualTo(beforeWalls[1]));
        }

        [Test]
        public void ReplaceWall_UnknownSurfaceDoesNotMutateExistingWalls()
        {
            var layout = CreateLayout();
            var before = SnapshotWalls(layout);

            Assert.Throws<ArgumentException>(() => layout.ReplaceWall(
                new WallAppearance("wall.front", "paint.sage", null)));

            Assert.That(SnapshotWalls(layout), Is.EqualTo(before));
        }

        [Test]
        public void ReplaceFloor_UnknownPositionDoesNotMutateExistingTiles()
        {
            var layout = CreateLayout();
            var before = SnapshotFloorTiles(layout);

            Assert.Throws<ArgumentException>(() => layout.ReplaceFloor(
                new FloorTileAppearance(
                    new GridPosition(8, 0),
                    "floor.tile.light",
                    SurfaceRotation.Degrees0)));

            Assert.That(SnapshotFloorTiles(layout), Is.EqualTo(before));
        }

        [Test]
        public void ReplaceFloor_ChangesOnlyEachTargetCornerAndPreservesEveryOtherTile()
        {
            var layout = CreateLayout();
            var before = SnapshotFloorTiles(layout);

            layout.ReplaceFloor(new FloorTileAppearance(
                new GridPosition(0, 0),
                "floor.tile.light",
                SurfaceRotation.Degrees90));
            layout.ReplaceFloor(new FloorTileAppearance(
                new GridPosition(7, 7),
                "floor.stone.dark",
                SurfaceRotation.Degrees270));

            Assert.That(layout.FloorTiles[new GridPosition(0, 0)].Rotation,
                Is.EqualTo(SurfaceRotation.Degrees90));
            Assert.That(layout.FloorTiles[new GridPosition(7, 7)].StyleId,
                Is.EqualTo("floor.stone.dark"));
            Assert.That(
                SnapshotFloorTiles(layout).Where(value =>
                    !value.StartsWith("0|0|", StringComparison.Ordinal) &&
                    !value.StartsWith("7|7|", StringComparison.Ordinal)),
                Is.EqualTo(before.Where(value =>
                    !value.StartsWith("0|0|", StringComparison.Ordinal) &&
                    !value.StartsWith("7|7|", StringComparison.Ordinal))));
        }

        [Test]
        public void ReplaceAllFloors_UpdatesExactlyExistingEightByEightKeysWithTheNewAppearance()
        {
            var layout = CreateLayout();
            var originalPositions = layout.FloorTiles.Keys.OrderBy(position => position.X)
                .ThenBy(position => position.Y).ToArray();

            layout.ReplaceAllFloors("floor.stone.dark", SurfaceRotation.Degrees180);

            Assert.That(layout.FloorTiles, Has.Count.EqualTo(64));
            Assert.That(
                layout.FloorTiles.Keys.OrderBy(position => position.X).ThenBy(position => position.Y),
                Is.EqualTo(originalPositions));
            Assert.That(layout.FloorTiles.Values.All(tile =>
                tile.StyleId == "floor.stone.dark" &&
                tile.Rotation == SurfaceRotation.Degrees180),
                Is.True);
        }

        [Test]
        public void ReplaceAllFloors_InvalidStyleOrRotationDoesNotMutateExistingTiles()
        {
            var layout = CreateLayout();
            var before = SnapshotFloorTiles(layout);

            Assert.Throws<ArgumentException>(() =>
                layout.ReplaceAllFloors("Floor Stone", SurfaceRotation.Degrees0));
            Assert.That(SnapshotFloorTiles(layout), Is.EqualTo(before));

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                layout.ReplaceAllFloors("floor.stone.dark", (SurfaceRotation)99));
            Assert.That(SnapshotFloorTiles(layout), Is.EqualTo(before));
        }

        [Test]
        public void CaptureSnapshot_RoundTripsInDeterministicOrderThroughJsonWithoutChangingValues()
        {
            var layout = new RoomSurfaceLayout(
                "room.main",
                CreateWalls(),
                CreateFloorTilesWithMixedAppearances());

            var first = layout.CaptureSnapshot();
            var firstJson = JsonUtility.ToJson(first);
            var second = layout.CaptureSnapshot();
            var secondJson = JsonUtility.ToJson(second);
            var deserialized = JsonUtility.FromJson<RoomSurfaceSnapshot>(firstJson);

            Assert.That(firstJson, Is.EqualTo(secondJson));
            Assert.That(first.RoomId, Is.EqualTo("room.main"));
            Assert.That(first.Walls.Select(entry => entry.SurfaceId), Is.EqualTo(
                new[] { "wall.back-left", "wall.back-right" }));
            Assert.That(deserialized.Walls[0].WainscotingStyleId, Is.EqualTo(string.Empty));
            var restored = RoomSurfaceLayout.FromSnapshot(deserialized);
            Assert.That(first.FloorTiles, Has.Count.EqualTo(64));
            Assert.That(first.FloorTiles.Select(entry => new GridPosition(entry.X, entry.Y)),
                Is.EqualTo(CreateOrderedFloorPositions()));
            Assert.That(restored.RoomId, Is.EqualTo(layout.RoomId));
            Assert.That(restored.Walls["wall.back-left"].WainscotingStyleId, Is.Null);
            Assert.That(SnapshotWalls(restored), Is.EqualTo(SnapshotWalls(layout)));
            Assert.That(SnapshotFloorTiles(restored), Is.EqualTo(SnapshotFloorTiles(layout)));
        }

        [Test]
        public void FromSnapshot_InvalidEntriesAreRejectedWithoutMutatingTheInputOrExposingPartialLayout()
        {
            var duplicateWall = CreateLayout().CaptureSnapshot();
            duplicateWall.Walls.Add(new WallAppearanceSnapshotEntry
            {
                SurfaceId = "wall.back-left",
                BaseStyleId = "paint.sage",
                WainscotingStyleId = null
            });
            AssertSnapshotRejectedWithoutInputMutation(duplicateWall);

            var invalidWall = CreateLayout().CaptureSnapshot();
            invalidWall.Walls[0].SurfaceId = "Wall Back";
            AssertSnapshotRejectedWithoutInputMutation(invalidWall);

            var emptyRoomId = CreateLayout().CaptureSnapshot();
            emptyRoomId.RoomId = "";
            AssertSnapshotRejectedWithoutInputMutation(emptyRoomId);

            var emptyWallSurfaceId = CreateLayout().CaptureSnapshot();
            emptyWallSurfaceId.Walls[0].SurfaceId = "";
            AssertSnapshotRejectedWithoutInputMutation(emptyWallSurfaceId);

            var emptyWallBaseStyleId = CreateLayout().CaptureSnapshot();
            emptyWallBaseStyleId.Walls[0].BaseStyleId = "";
            AssertSnapshotRejectedWithoutInputMutation(emptyWallBaseStyleId);

            var emptyFloorStyleId = CreateLayout().CaptureSnapshot();
            emptyFloorStyleId.FloorTiles[0].StyleId = "";
            AssertSnapshotRejectedWithoutInputMutation(emptyFloorStyleId);

            var duplicateFloor = CreateLayout().CaptureSnapshot();
            duplicateFloor.FloorTiles[63].X = 0;
            duplicateFloor.FloorTiles[63].Y = 0;
            AssertSnapshotRejectedWithoutInputMutation(duplicateFloor);

            var missingFloor = CreateLayout().CaptureSnapshot();
            missingFloor.FloorTiles.RemoveAt(63);
            AssertSnapshotRejectedWithoutInputMutation(missingFloor);

            var outsideFloor = CreateLayout().CaptureSnapshot();
            outsideFloor.FloorTiles[63].X = 8;
            outsideFloor.FloorTiles[63].Y = 7;
            AssertSnapshotRejectedWithoutInputMutation(outsideFloor);
        }

        [Test]
        public void ApplySnapshot_ValidCandidateSwapsCompleteWallAndFloorState()
        {
            var layout = CreateLayout();
            var retainedWalls = layout.Walls;
            var retainedFloors = layout.FloorTiles;
            var snapshot = layout.CaptureSnapshot();
            foreach (var wall in snapshot.Walls)
            {
                wall.BaseStyleId = "paint.atomic";
                wall.WainscotingStyleId = "wainscot.atomic";
            }

            foreach (var floor in snapshot.FloorTiles)
            {
                floor.StyleId = "floor.atomic";
                floor.Rotation = SurfaceRotation.Degrees270;
            }

            var inputBefore = JsonUtility.ToJson(snapshot);

            layout.ApplySnapshot(snapshot);

            Assert.That(layout.Walls.Values.Select(wall => wall.BaseStyleId),
                Is.All.EqualTo("paint.atomic"));
            Assert.That(layout.Walls.Values.Select(wall => wall.WainscotingStyleId),
                Is.All.EqualTo("wainscot.atomic"));
            Assert.That(layout.FloorTiles.Values.Select(floor => floor.StyleId),
                Is.All.EqualTo("floor.atomic"));
            Assert.That(layout.FloorTiles.Values.Select(floor => floor.Rotation),
                Is.All.EqualTo(SurfaceRotation.Degrees270));
            Assert.That(retainedWalls.Values.Select(wall => wall.BaseStyleId),
                Is.EquivalentTo(new[] { "paint.cream", "wallpaper.sage.sprig" }));
            Assert.That(retainedFloors.Values.Select(floor => floor.StyleId),
                Is.All.EqualTo("floor.wood.warm"));
            Assert.That(JsonUtility.ToJson(snapshot), Is.EqualTo(inputBefore));
        }

        [Test]
        public void ApplySnapshot_InvalidOrDifferentRoomCandidateLeavesConfirmedStateExact()
        {
            var layout = CreateLayout();
            var confirmedBefore = SnapshotLayout(layout);

            var invalid = layout.CaptureSnapshot();
            invalid.FloorTiles.RemoveAt(63);
            var invalidBefore = JsonUtility.ToJson(invalid);
            Assert.That(() => layout.ApplySnapshot(invalid),
                Throws.InstanceOf<ArgumentException>());
            Assert.That(SnapshotLayout(layout), Is.EqualTo(confirmedBefore));
            Assert.That(JsonUtility.ToJson(invalid), Is.EqualTo(invalidBefore));

            var differentRoom = layout.CaptureSnapshot();
            differentRoom.RoomId = "room.other";
            var differentRoomBefore = JsonUtility.ToJson(differentRoom);
            Assert.That(() => layout.ApplySnapshot(differentRoom),
                Throws.InstanceOf<ArgumentException>());
            Assert.That(SnapshotLayout(layout), Is.EqualTo(confirmedBefore));
            Assert.That(JsonUtility.ToJson(differentRoom),
                Is.EqualTo(differentRoomBefore));
        }

        [Test]
        public void ApplySnapshot_ForeignWallIdentitySetIsRejectedWithoutChangingStateOrViews()
        {
            var layout = CreateLayout();
            var retainedWalls = layout.Walls;
            var retainedFloors = layout.FloorTiles;
            var confirmedBefore = SnapshotLayout(layout);
            var retainedWallsBefore = retainedWalls
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => string.Join(
                    "|",
                    pair.Key,
                    pair.Value.BaseStyleId,
                    pair.Value.WainscotingStyleId ?? "<none>"))
                .ToArray();
            var retainedFloorsBefore = retainedFloors
                .OrderBy(pair => pair.Key.X)
                .ThenBy(pair => pair.Key.Y)
                .Select(pair => string.Join(
                    "|",
                    pair.Key.X,
                    pair.Key.Y,
                    pair.Value.StyleId,
                    pair.Value.Rotation))
                .ToArray();
            var foreign = layout.CaptureSnapshot();
            foreign.Walls[0].SurfaceId = "wall.foreign-left";
            foreign.Walls[1].SurfaceId = "wall.foreign-right";
            var inputBefore = JsonUtility.ToJson(foreign);

            Assert.That(() => layout.ApplySnapshot(foreign),
                Throws.InstanceOf<ArgumentException>());

            Assert.That(SnapshotLayout(layout), Is.EqualTo(confirmedBefore));
            Assert.That(layout.Walls, Is.SameAs(retainedWalls));
            Assert.That(layout.FloorTiles, Is.SameAs(retainedFloors));
            Assert.That(retainedWalls
                    .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => string.Join(
                        "|",
                        pair.Key,
                        pair.Value.BaseStyleId,
                        pair.Value.WainscotingStyleId ?? "<none>")),
                Is.EqualTo(retainedWallsBefore));
            Assert.That(retainedFloors
                    .OrderBy(pair => pair.Key.X)
                    .ThenBy(pair => pair.Key.Y)
                    .Select(pair => string.Join(
                        "|",
                        pair.Key.X,
                        pair.Key.Y,
                        pair.Value.StyleId,
                        pair.Value.Rotation)),
                Is.EqualTo(retainedFloorsBefore));
            Assert.That(JsonUtility.ToJson(foreign), Is.EqualTo(inputBefore));
        }

        [Test]
        public void ApplySnapshot_SameWallIdentitySetInDifferentOrderIsAccepted()
        {
            var layout = CreateLayout();
            var reordered = layout.CaptureSnapshot();
            reordered.Walls.Reverse();
            reordered.Walls.Single(wall => wall.SurfaceId == "wall.back-left")
                .BaseStyleId = "paint.reordered-left";
            reordered.Walls.Single(wall => wall.SurfaceId == "wall.back-right")
                .BaseStyleId = "paint.reordered-right";

            Assert.DoesNotThrow(() => layout.ApplySnapshot(reordered));

            Assert.That(layout.Walls["wall.back-left"].BaseStyleId,
                Is.EqualTo("paint.reordered-left"));
            Assert.That(layout.Walls["wall.back-right"].BaseStyleId,
                Is.EqualTo("paint.reordered-right"));
        }

        private static RoomSurfaceLayout CreateLayout()
        {
            return new RoomSurfaceLayout("room.main", CreateWalls(), CreateFloorTiles());
        }

        private static WallAppearance[] CreateWalls()
        {
            return new[]
            {
                new WallAppearance("wall.back-left", "paint.cream", null),
                new WallAppearance("wall.back-right", "wallpaper.sage.sprig", "wainscot.warm.white")
            };
        }

        private static IEnumerable<FloorTileAppearance> CreateFloorTiles()
        {
            for (var x = 0; x < 8; x++)
            {
                for (var y = 0; y < 8; y++)
                {
                    yield return new FloorTileAppearance(
                        new GridPosition(x, y),
                        "floor.wood.warm",
                        SurfaceRotation.Degrees0);
                }
            }
        }

        private static IEnumerable<FloorTileAppearance> CreateFloorTilesWithMixedAppearances()
        {
            var rotations = new[]
            {
                SurfaceRotation.Degrees0,
                SurfaceRotation.Degrees90,
                SurfaceRotation.Degrees180,
                SurfaceRotation.Degrees270
            };

            for (var x = 0; x < 8; x++)
            {
                for (var y = 0; y < 8; y++)
                {
                    var index = (x + y) % rotations.Length;
                    yield return new FloorTileAppearance(
                        new GridPosition(x, y),
                        index % 2 == 0 ? "floor.wood.warm" : "floor.stone.dark",
                        rotations[index]);
                }
            }
        }

        private static GridPosition[] CreateOrderedFloorPositions()
        {
            return Enumerable.Range(0, 8)
                .SelectMany(x => Enumerable.Range(0, 8)
                    .Select(y => new GridPosition(x, y)))
                .ToArray();
        }

        private static void AssertSnapshotRejectedWithoutInputMutation(RoomSurfaceSnapshot snapshot)
        {
            var before = JsonUtility.ToJson(snapshot);

            Assert.That(
                () => RoomSurfaceLayout.FromSnapshot(snapshot),
                Throws.InstanceOf<ArgumentException>());

            Assert.That(JsonUtility.ToJson(snapshot), Is.EqualTo(before));
        }

        private static void AssertConstructorRejectsWithoutMutatingSources(
            List<WallAppearance> walls,
            List<FloorTileAppearance> floorTiles)
        {
            var wallsBefore = SnapshotWallSource(walls);
            var floorTilesBefore = SnapshotFloorTileSource(floorTiles);

            Assert.That(
                () => new RoomSurfaceLayout("room.main", walls, floorTiles),
                Throws.InstanceOf<ArgumentException>());

            Assert.That(SnapshotWallSource(walls), Is.EqualTo(wallsBefore));
            Assert.That(SnapshotFloorTileSource(floorTiles), Is.EqualTo(floorTilesBefore));
        }

        private static string[] SnapshotWallSource(IEnumerable<WallAppearance> walls)
        {
            return walls.Select(wall => string.Join(
                "|",
                wall.SurfaceId,
                wall.BaseStyleId,
                wall.WainscotingStyleId ?? "<none>"))
                .ToArray();
        }

        private static string[] SnapshotFloorTileSource(
            IEnumerable<FloorTileAppearance> floorTiles)
        {
            return floorTiles.Select(tile => string.Join(
                "|",
                tile.Position.X,
                tile.Position.Y,
                tile.StyleId,
                tile.Rotation))
                .ToArray();
        }

        private static string[] SnapshotLayout(RoomSurfaceLayout layout)
        {
            return SnapshotWalls(layout).Concat(SnapshotFloorTiles(layout)).ToArray();
        }

        private static string[] SnapshotWalls(RoomSurfaceLayout layout)
        {
            return layout.Walls.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => string.Join(
                    "|",
                    pair.Key,
                    pair.Value.BaseStyleId,
                    pair.Value.WainscotingStyleId ?? "<none>"))
                .ToArray();
        }

        private static string[] SnapshotFloorTiles(RoomSurfaceLayout layout)
        {
            return layout.FloorTiles.OrderBy(pair => pair.Key.X)
                .ThenBy(pair => pair.Key.Y)
                .Select(pair => string.Join(
                    "|",
                    pair.Key.X,
                    pair.Key.Y,
                    pair.Value.StyleId,
                    pair.Value.Rotation))
                .ToArray();
        }
    }
}

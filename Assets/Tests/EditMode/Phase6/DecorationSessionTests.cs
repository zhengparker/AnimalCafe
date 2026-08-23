using AnimalCafe.Decoration;
using AnimalCafe.Layout;
using NUnit.Framework;

namespace AnimalCafe.Tests.Phase6
{
    public sealed class DecorationSessionTests
    {
        private const string ExistingInstanceId =
            "e4ca5e4ea4984b27ba6e2e0545054966";
        private const string OtherInstanceId =
            "f5db6f5fb5a94c38cb7f3f1656165077";

        [Test]
        public void Enter_IsIdempotentAndLeavesTheSessionBrowsing()
        {
            var session = CreateSession();

            session.Enter();
            session.Enter();

            Assert.That(session.State, Is.EqualTo(DecorationSessionState.BrowsingCatalogue));
            Assert.That(session.ActivePreview, Is.Null);
        }

        [Test]
        public void ExitFromBrowsing_ClosesWithoutChangingTheLayout()
        {
            var layout = CreateLayout();
            var session = new DecorationSession(layout);
            session.Enter();

            session.Exit();

            Assert.That(session.State, Is.EqualTo(DecorationSessionState.Closed));
            Assert.That(session.ActivePreview, Is.Null);
            Assert.That(layout.FurnitureInstances, Is.Empty);
            Assert.That(layout.OccupiedCellCount, Is.EqualTo(0));
        }

        [Test]
        public void ExitWhileEditingExisting_CancelsThePendingMoveAndRotation()
        {
            var layout = CreateLayoutWithExistingFurniture();
            var session = new DecorationSession(layout);
            session.Enter();
            Assert.That(session.BeginExisting(ExistingInstanceId).Succeeded, Is.True);
            Assert.That(session.MovePreview(new GridPosition(4, 4)).Succeeded, Is.True);
            Assert.That(session.RotatePreview().Succeeded, Is.True);

            session.Exit();

            Assert.That(session.State, Is.EqualTo(DecorationSessionState.Closed));
            Assert.That(session.ActivePreview, Is.Null);
            Assert.That(layout.TryGetFurnitureInstance(ExistingInstanceId, out var instance), Is.True);
            Assert.That(instance.Position, Is.EqualTo(new GridPosition(1, 1)));
            Assert.That(instance.Rotation, Is.EqualTo(FurnitureRotation.Degrees0));
        }

        [Test]
        public void ExitWhilePreviewingNewFurniture_DiscardsThePreviewWithoutCreatingAnInstance()
        {
            var layout = CreateLayout();
            var session = new DecorationSession(layout);
            session.Enter();
            session.BeginNew("counter.preset.1x2", new GridPosition(2, 2));
            Assert.That(session.MovePreview(new GridPosition(4, 4)).Succeeded, Is.True);

            session.Exit();

            Assert.That(session.State, Is.EqualTo(DecorationSessionState.Closed));
            Assert.That(session.ActivePreview, Is.Null);
            Assert.That(layout.FurnitureInstances, Is.Empty);
            Assert.That(layout.OccupiedCellCount, Is.EqualTo(0));
        }

        [Test]
        public void BeginExisting_CreatesOnePreviewFromTheCurrentLayoutSnapshotWithoutMutatingIt()
        {
            var layout = CreateLayoutWithExistingFurniture();
            var session = new DecorationSession(layout);
            session.Enter();

            var result = session.BeginExisting(ExistingInstanceId);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(session.State, Is.EqualTo(DecorationSessionState.EditingExistingFurniture));
            Assert.That(session.ActivePreview.SourceInstanceId, Is.EqualTo(ExistingInstanceId));
            Assert.That(session.ActivePreview.DefinitionId, Is.EqualTo("counter.preset.1x2"));
            Assert.That(session.ActivePreview.OriginalPosition, Is.EqualTo(new GridPosition(1, 1)));
            Assert.That(session.ActivePreview.OriginalRotation, Is.EqualTo(FurnitureRotation.Degrees0));
            Assert.That(session.ActivePreview.ProposedPosition, Is.EqualTo(new GridPosition(1, 1)));
            Assert.That(session.ActivePreview.ProposedRotation, Is.EqualTo(FurnitureRotation.Degrees0));
            Assert.That(layout.TryGetFurnitureInstance(ExistingInstanceId, out var instance), Is.True);
            Assert.That(instance.Position, Is.EqualTo(new GridPosition(1, 1)));
            Assert.That(instance.Rotation, Is.EqualTo(FurnitureRotation.Degrees0));
        }

        [Test]
        public void BeginExisting_ReplacesAnActiveNewPreviewWithoutCreatingAFormalInstance()
        {
            var layout = CreateLayoutWithExistingFurniture();
            var session = new DecorationSession(layout);
            session.Enter();
            session.BeginNew("counter.preset.1x1", new GridPosition(4, 4));

            var result = session.BeginExisting(ExistingInstanceId);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(session.State, Is.EqualTo(DecorationSessionState.EditingExistingFurniture));
            Assert.That(session.ActivePreview.SourceInstanceId, Is.EqualTo(ExistingInstanceId));
            Assert.That(layout.FurnitureInstances, Has.Count.EqualTo(1));
            Assert.That(layout.OccupiedCellCount, Is.EqualTo(2));
        }

        [Test]
        public void BeginNew_WhenPreferredCellIsOccupied_UsesTheNearestValidEmptyCell()
        {
            var layout = CreateLayout();
            Assert.That(layout.PlaceFurniture(FurnitureInstance.Restore(
                ExistingInstanceId,
                "counter.preset.1x1",
                new GridPosition(4, 4),
                FurnitureRotation.Degrees0)).Succeeded, Is.True);
            var session = new DecorationSession(layout);
            session.Enter();

            session.BeginNew("counter.preset.1x1", new GridPosition(4, 4));

            Assert.That(session.ActivePreview.ProposedPosition,
                Is.EqualTo(new GridPosition(3, 4)));
            Assert.That(session.ActivePreview.PlacementResult.Succeeded, Is.True);
        }

        [Test]
        public void BeginNew_WhenTheFloorHasNoValidSpace_KeepsAnInvalidPreferredPreview()
        {
            var layout = CreateOneCellLayout();
            Assert.That(layout.PlaceFurniture(FurnitureInstance.Restore(
                ExistingInstanceId,
                "counter.preset.1x1",
                new GridPosition(0, 0),
                FurnitureRotation.Degrees0)).Succeeded, Is.True);
            var session = new DecorationSession(layout);
            session.Enter();

            session.BeginNew("counter.preset.1x1", new GridPosition(0, 0));

            Assert.That(session.ActivePreview.ProposedPosition,
                Is.EqualTo(new GridPosition(0, 0)));
            Assert.That(session.ActivePreview.PlacementResult.FailureReason,
                Is.EqualTo(PlacementFailureReason.Overlap));
        }

        [Test]
        public void BeginExisting_ReplacesAnotherExistingPreviewAndLeavesBothFormalInstancesUnchanged()
        {
            var layout = CreateLayoutWithTwoExistingFurniture();
            var session = new DecorationSession(layout);
            session.Enter();
            Assert.That(session.BeginExisting(ExistingInstanceId).Succeeded, Is.True);
            Assert.That(session.MovePreview(new GridPosition(2, 4)).Succeeded, Is.True);

            var result = session.BeginExisting(OtherInstanceId);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(session.ActivePreview.SourceInstanceId, Is.EqualTo(OtherInstanceId));
            Assert.That(layout.TryGetFurnitureInstance(ExistingInstanceId, out var first), Is.True);
            Assert.That(first.Position, Is.EqualTo(new GridPosition(1, 1)));
            Assert.That(layout.TryGetFurnitureInstance(OtherInstanceId, out var second), Is.True);
            Assert.That(second.Position, Is.EqualTo(new GridPosition(5, 1)));
        }

        [Test]
        public void MoveAndRotatePreview_ReplaceTheImmutablePreviewWithoutChangingTheLayout()
        {
            var layout = CreateLayoutWithExistingFurniture();
            var session = new DecorationSession(layout);
            session.Enter();
            Assert.That(session.BeginExisting(ExistingInstanceId).Succeeded, Is.True);
            var originalPreview = session.ActivePreview;

            var move = session.MovePreview(new GridPosition(4, 4));
            var movedPreview = session.ActivePreview;
            var rotate = session.RotatePreview();

            Assert.That(move.Succeeded, Is.True);
            Assert.That(rotate.Succeeded, Is.True);
            Assert.That(movedPreview, Is.Not.SameAs(originalPreview));
            Assert.That(session.ActivePreview, Is.Not.SameAs(movedPreview));
            Assert.That(session.ActivePreview.ProposedPosition, Is.EqualTo(new GridPosition(4, 4)));
            Assert.That(session.ActivePreview.ProposedRotation, Is.EqualTo(FurnitureRotation.Degrees90));
            Assert.That(layout.TryGetFurnitureInstance(ExistingInstanceId, out var instance), Is.True);
            Assert.That(instance.Position, Is.EqualTo(new GridPosition(1, 1)));
            Assert.That(instance.Rotation, Is.EqualTo(FurnitureRotation.Degrees0));
        }

        [TestCase(FurnitureRotation.Degrees0, 3, 2, 2, 3)]
        [TestCase(FurnitureRotation.Degrees90, 2, 3, 3, 2)]
        [TestCase(FurnitureRotation.Degrees180, 3, 2, 2, 3)]
        [TestCase(FurnitureRotation.Degrees270, 2, 3, 3, 2)]
        public void RotatePreview_OneByThreePreservesPriorVisualCenter(
            FurnitureRotation startingRotation,
            int startX,
            int startY,
            int expectedX,
            int expectedY)
        {
            var session = CreateRotationSession(
                "counter.preset.1x3",
                new GridSize(1, 3),
                new GridPosition(startX, startY),
                startingRotation);
            var beforeCells = RotationLayout.GetFurnitureFootprintCells(
                "counter.preset.1x3",
                session.ActivePreview.ProposedPosition,
                session.ActivePreview.ProposedRotation);
            var beforeCenter = FootprintCenter(beforeCells);

            var result = session.RotatePreview();

            var after = session.ActivePreview;
            var afterCells = RotationLayout.GetFurnitureFootprintCells(
                after.DefinitionId,
                after.ProposedPosition,
                after.ProposedRotation);
            Assert.That(result.Succeeded, Is.True);
            Assert.That(after.ProposedPosition, Is.EqualTo(new GridPosition(expectedX, expectedY)));
            Assert.That(FootprintCenter(afterCells), Is.EqualTo(beforeCenter));
        }

        [TestCase("counter.preset.1x2", 1, 2)]
        [TestCase("counter.preset.2x3", 2, 3)]
        public void RotatePreview_ParityTieUsesDeterministicNearestCell(
            string definitionId,
            int width,
            int height)
        {
            var origin = new GridPosition(3, 3);
            var session = CreateRotationSession(
                definitionId,
                new GridSize(width, height),
                origin,
                FurnitureRotation.Degrees0);

            var result = session.RotatePreview();

            Assert.That(result.Succeeded, Is.True);
            Assert.That(
                session.ActivePreview.ProposedPosition,
                Is.EqualTo(origin),
                "An exact half-cell tie must truncate toward zero instead of adding a cell.");
        }

        [TestCase(FurnitureRotation.Degrees0)]
        [TestCase(FurnitureRotation.Degrees90)]
        [TestCase(FurnitureRotation.Degrees180)]
        [TestCase(FurnitureRotation.Degrees270)]
        public void RotatePreview_FourTurnsRestoreOriginalPositionAndRotation(
            FurnitureRotation startingRotation)
        {
            var origin = new GridPosition(3, 3);
            var session = CreateRotationSession(
                "counter.preset.1x3",
                new GridSize(1, 3),
                origin,
                startingRotation);

            for (var turn = 0; turn < 4; turn++)
            {
                Assert.That(session.RotatePreview().Succeeded, Is.True);
            }

            Assert.That(session.ActivePreview.ProposedPosition, Is.EqualTo(origin));
            Assert.That(session.ActivePreview.ProposedRotation, Is.EqualTo(startingRotation));
        }

        [TestCase(FurnitureRotation.Degrees180, 0, 2, -1, 3)]
        [TestCase(FurnitureRotation.Degrees0, 7, 2, 6, 3)]
        [TestCase(FurnitureRotation.Degrees90, 2, 0, 3, -1)]
        [TestCase(FurnitureRotation.Degrees90, 2, 7, 3, 6)]
        public void RotatePreview_NearBoundsKeepsNearestInvalidCandidateVisible(
            FurnitureRotation startingRotation,
            int startX,
            int startY,
            int expectedX,
            int expectedY)
        {
            var session = CreateRotationSession(
                "counter.preset.1x3",
                new GridSize(1, 3),
                new GridPosition(startX, startY),
                startingRotation);

            var result = session.RotatePreview();

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailureReason, Is.EqualTo(PlacementFailureReason.OutOfLayoutBounds));
            Assert.That(session.ActivePreview, Is.Not.Null);
            Assert.That(
                session.ActivePreview.ProposedPosition,
                Is.EqualTo(new GridPosition(expectedX, expectedY)));
        }

        [Test]
        public void ConfirmPreview_NewFurnitureCommitsOnceAndSecondConfirmDoesNotCreateAnotherInstance()
        {
            var layout = CreateLayout();
            var session = new DecorationSession(layout);
            session.Enter();
            session.BeginNew("counter.preset.1x2", new GridPosition(2, 2));

            var firstConfirm = session.ConfirmPreview();
            var secondConfirm = session.ConfirmPreview();

            Assert.That(firstConfirm.Succeeded, Is.True);
            Assert.That(secondConfirm.Succeeded, Is.True);
            Assert.That(session.State, Is.EqualTo(DecorationSessionState.BrowsingCatalogue));
            Assert.That(session.ActivePreview, Is.Null);
            Assert.That(layout.FurnitureInstances, Has.Count.EqualTo(1));
            Assert.That(layout.OccupiedCellCount, Is.EqualTo(2));
            Assert.That(layout.FurnitureInstances[0].DefinitionId, Is.EqualTo("counter.preset.1x2"));
            Assert.That(layout.FurnitureInstances[0].Position, Is.EqualTo(new GridPosition(2, 2)));
            Assert.That(layout.FurnitureInstances[0].Rotation, Is.EqualTo(FurnitureRotation.Degrees0));
        }

        [Test]
        public void ConfirmPreview_ExistingFurnitureUpdatesItsPlacementAndKeepsItsIdentity()
        {
            var layout = CreateLayoutWithExistingFurniture();
            var session = new DecorationSession(layout);
            session.Enter();
            Assert.That(session.BeginExisting(ExistingInstanceId).Succeeded, Is.True);
            Assert.That(session.MovePreview(new GridPosition(4, 4)).Succeeded, Is.True);
            Assert.That(session.RotatePreview().Succeeded, Is.True);

            var result = session.ConfirmPreview();

            Assert.That(result.Succeeded, Is.True);
            Assert.That(session.State, Is.EqualTo(DecorationSessionState.BrowsingCatalogue));
            Assert.That(session.ActivePreview, Is.Null);
            Assert.That(layout.FurnitureInstances, Has.Count.EqualTo(1));
            Assert.That(layout.TryGetFurnitureInstance(ExistingInstanceId, out var updated), Is.True);
            Assert.That(updated.Position, Is.EqualTo(new GridPosition(4, 4)));
            Assert.That(updated.Rotation, Is.EqualTo(FurnitureRotation.Degrees90));
        }

        [Test]
        public void ConfirmPreview_InvalidCandidateKeepsThePreviewAndFormalLayoutUnchanged()
        {
            var layout = CreateLayout();
            var session = new DecorationSession(layout);
            session.Enter();
            session.BeginNew("counter.preset.1x2", new GridPosition(2, 2));
            Assert.That(
                session.MovePreview(new GridPosition(7, 7)).FailureReason,
                Is.EqualTo(PlacementFailureReason.OutOfLayoutBounds));

            var result = session.ConfirmPreview();

            Assert.That(result.FailureReason, Is.EqualTo(PlacementFailureReason.OutOfLayoutBounds));
            Assert.That(session.State, Is.EqualTo(DecorationSessionState.PreviewingNewFurniture));
            Assert.That(session.ActivePreview, Is.Not.Null);
            Assert.That(layout.FurnitureInstances, Is.Empty);
            Assert.That(layout.OccupiedCellCount, Is.EqualTo(0));
        }

        [Test]
        public void CancelPreview_DiscardsNewPreviewWithoutAffectingPriorConfirmedFurniture()
        {
            var layout = CreateLayout();
            var session = new DecorationSession(layout);
            session.Enter();
            session.BeginNew("counter.preset.1x1", new GridPosition(1, 1));
            Assert.That(session.ConfirmPreview().Succeeded, Is.True);
            session.BeginNew("counter.preset.1x2", new GridPosition(4, 4));

            session.CancelPreview();

            Assert.That(session.State, Is.EqualTo(DecorationSessionState.BrowsingCatalogue));
            Assert.That(session.ActivePreview, Is.Null);
            Assert.That(layout.FurnitureInstances, Has.Count.EqualTo(1));
            Assert.That(layout.FurnitureInstances[0].Position, Is.EqualTo(new GridPosition(1, 1)));
            Assert.That(layout.OccupiedCellCount, Is.EqualTo(1));
        }

        [Test]
        public void BeginStoreConfirmationAndDismiss_ReturnToTheExistingPreviewWithoutMutatingTheLayout()
        {
            var layout = CreateLayoutWithExistingFurniture();
            var session = new DecorationSession(layout);
            session.Enter();
            Assert.That(session.BeginExisting(ExistingInstanceId).Succeeded, Is.True);
            Assert.That(session.MovePreview(new GridPosition(4, 4)).Succeeded, Is.True);

            var started = session.BeginStoreConfirmation();
            session.DismissStoreConfirmation();

            Assert.That(started, Is.True);
            Assert.That(session.State, Is.EqualTo(DecorationSessionState.EditingExistingFurniture));
            Assert.That(session.ActivePreview.SourceInstanceId, Is.EqualTo(ExistingInstanceId));
            Assert.That(session.ActivePreview.ProposedPosition, Is.EqualTo(new GridPosition(4, 4)));
            Assert.That(layout.TryGetFurnitureInstance(ExistingInstanceId, out var instance), Is.True);
            Assert.That(instance.Position, Is.EqualTo(new GridPosition(1, 1)));
        }

        [Test]
        public void ConfirmStore_RemovesTheSelectedFurnitureOnceAndIgnoresTheSecondRequest()
        {
            var layout = CreateLayoutWithExistingFurniture();
            var session = new DecorationSession(layout);
            session.Enter();
            Assert.That(session.BeginExisting(ExistingInstanceId).Succeeded, Is.True);
            Assert.That(session.BeginStoreConfirmation(), Is.True);

            var firstConfirm = session.ConfirmStore();
            var secondConfirm = session.ConfirmStore();

            Assert.That(firstConfirm.Succeeded, Is.True);
            Assert.That(secondConfirm.Succeeded, Is.True);
            Assert.That(session.State, Is.EqualTo(DecorationSessionState.BrowsingCatalogue));
            Assert.That(session.ActivePreview, Is.Null);
            Assert.That(layout.TryGetFurnitureInstance(ExistingInstanceId, out _), Is.False);
            Assert.That(layout.FurnitureInstances, Is.Empty);
            Assert.That(layout.OccupiedCellCount, Is.EqualTo(0));
        }

        [Test]
        public void StoreIsUnavailableForNewPreviewAndConfirmStoreDoesNotMutateTheLayout()
        {
            var layout = CreateLayout();
            var session = new DecorationSession(layout);
            session.Enter();
            session.BeginNew("counter.preset.1x1", new GridPosition(2, 2));

            var started = session.BeginStoreConfirmation();
            var result = session.ConfirmStore();

            Assert.That(started, Is.False);
            Assert.That(result.Succeeded, Is.True);
            Assert.That(session.State, Is.EqualTo(DecorationSessionState.PreviewingNewFurniture));
            Assert.That(session.ActivePreview.IsNew, Is.True);
            Assert.That(layout.FurnitureInstances, Is.Empty);
        }

        [TestCase(PlacementFailureReason.Overlap, PlacementFeedbackKey.Occupied)]
        [TestCase(PlacementFailureReason.OutOfLayoutBounds, PlacementFeedbackKey.OutsideUnlockedArea)]
        [TestCase(PlacementFailureReason.OutOfUnlockedRegion, PlacementFeedbackKey.OutsideUnlockedArea)]
        [TestCase(PlacementFailureReason.LockedCell, PlacementFeedbackKey.Locked)]
        [TestCase(PlacementFailureReason.ReservedEntranceClearance, PlacementFeedbackKey.EntranceClearance)]
        [TestCase(PlacementFailureReason.UnsupportedPlacementSurface, PlacementFeedbackKey.UnsupportedSurface)]
        [TestCase(PlacementFailureReason.InstanceNotFound, PlacementFeedbackKey.MissingInstance)]
        public void PlacementFeedbackMapper_MapsLayoutReasonsToStableFeedbackKeys(
            PlacementFailureReason reason,
            PlacementFeedbackKey expected)
        {
            Assert.That(
                PlacementFeedbackMapper.Map(PlacementResult.Failure(reason)),
                Is.EqualTo(expected));
        }

        [Test]
        public void PlacementFeedbackMapper_MapsRealBlockedReservationToBlocked()
        {
            var layout = CreateLayout();
            layout.AddReservation(new LayoutReservation(
                "blocked.service",
                LayoutReservationType.Blocked,
                new GridPosition(3, 3),
                new GridSize(1, 1)));

            var result = layout.ValidateFurniturePlacement(
                "counter.preset.1x1",
                new GridPosition(3, 3),
                FurnitureRotation.Degrees0);

            Assert.That(
                result.FailureReason,
                Is.EqualTo(PlacementFailureReason.Blocked));
            Assert.That(
                PlacementFeedbackMapper.Map(result),
                Is.EqualTo(PlacementFeedbackKey.Blocked));
        }

        [Test]
        public void PlacementFeedbackMapper_MapsSuccessfulPlacementToNone()
        {
            Assert.That(
                PlacementFeedbackMapper.Map(PlacementResult.Success()),
                Is.EqualTo(PlacementFeedbackKey.None));
        }

        private static DecorationSession CreateSession()
        {
            return new DecorationSession(CreateLayout());
        }

        private static CafeLayout CreateLayoutWithExistingFurniture()
        {
            var layout = CreateLayout();
            Assert.That(layout.PlaceFurniture(FurnitureInstance.Restore(
                ExistingInstanceId,
                "counter.preset.1x2",
                new GridPosition(1, 1),
                FurnitureRotation.Degrees0)).Succeeded, Is.True);
            return layout;
        }

        private static CafeLayout CreateLayoutWithTwoExistingFurniture()
        {
            var layout = CreateLayoutWithExistingFurniture();
            Assert.That(layout.PlaceFurniture(FurnitureInstance.Restore(
                OtherInstanceId,
                "counter.preset.1x1",
                new GridPosition(5, 1),
                FurnitureRotation.Degrees0)).Succeeded, Is.True);
            return layout;
        }

        private static CafeLayout CreateLayout()
        {
            var layout = new CafeLayout(
                new GridSettings(1f),
                new FurnitureDefinitionCatalog(new[]
                {
                    new FurnitureDefinition(
                        "counter.preset.1x1",
                        "1 x 1 Counter Module",
                        new GridSize(1, 1),
                        PlacementSurfaceType.Floor),
                    new FurnitureDefinition(
                        "counter.preset.1x2",
                        "1 x 2 Counter Module",
                        new GridSize(1, 2),
                        PlacementSurfaceType.Floor)
                }),
                new LayoutBounds(
                    new GridPosition(0, 0),
                    new GridSize(8, 8)));
            layout.AddRegion(new LayoutRegion(
                "region.main",
                new GridPosition(0, 0),
                new GridSize(8, 8),
                LayoutZoneType.Interior));
            return layout;
        }

        private static CafeLayout CreateOneCellLayout()
        {
            var layout = new CafeLayout(
                new GridSettings(1f),
                new FurnitureDefinitionCatalog(new[]
                {
                    new FurnitureDefinition(
                        "counter.preset.1x1",
                        "1 x 1 Counter Module",
                        new GridSize(1, 1),
                        PlacementSurfaceType.Floor)
                }),
                new LayoutBounds(new GridPosition(0, 0), new GridSize(1, 1)));
            layout.AddRegion(new LayoutRegion(
                "region.one-cell",
                new GridPosition(0, 0),
                new GridSize(1, 1),
                LayoutZoneType.Interior));
            return layout;
        }

        private static CafeLayout RotationLayout { get; set; }

        private static DecorationSession CreateRotationSession(
            string definitionId,
            GridSize footprint,
            GridPosition position,
            FurnitureRotation rotation)
        {
            var layout = new CafeLayout(
                new GridSettings(1f),
                new FurnitureDefinitionCatalog(new[]
                {
                    new FurnitureDefinition(
                        definitionId,
                        definitionId,
                        footprint,
                        PlacementSurfaceType.Floor)
                }),
                new LayoutBounds(new GridPosition(0, 0), new GridSize(8, 8)));
            layout.AddRegion(new LayoutRegion(
                "region.rotation",
                new GridPosition(0, 0),
                new GridSize(8, 8),
                LayoutZoneType.Interior));
            Assert.That(layout.PlaceFurniture(FurnitureInstance.Restore(
                ExistingInstanceId,
                definitionId,
                position,
                rotation)).Succeeded, Is.True);

            var session = new DecorationSession(layout);
            session.Enter();
            Assert.That(session.BeginExisting(ExistingInstanceId).Succeeded, Is.True);
            RotationLayout = layout;
            return session;
        }

        private static (double x, double y) FootprintCenter(
            System.Collections.Generic.IReadOnlyList<GridPosition> cells)
        {
            var minX = int.MaxValue;
            var maxX = int.MinValue;
            var minY = int.MaxValue;
            var maxY = int.MinValue;
            foreach (var cell in cells)
            {
                minX = System.Math.Min(minX, cell.X);
                maxX = System.Math.Max(maxX, cell.X);
                minY = System.Math.Min(minY, cell.Y);
                maxY = System.Math.Max(maxY, cell.Y);
            }

            return ((minX + maxX + 1) * 0.5d, (minY + maxY + 1) * 0.5d);
        }
    }
}

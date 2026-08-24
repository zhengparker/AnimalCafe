using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using AnimalCafe.Interaction;
using AnimalCafe.Camera;
using AnimalCafe.Decoration;
using AnimalCafe.Decoration.Input;
using AnimalCafe.Input;
using AnimalCafe.UI.Foundation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Utils;
using InputTouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace AnimalCafe.Tests.PlayMode
{
    public sealed class Phase6DecorationTouchRouterTests
    {
        [Test]
        public void DefaultFrameAndResult_AreNoneAndCommandFree()
        {
            var router = new DecorationTouchRouter(6f, 24f);
            var classifier = new RecordingClassifier(new DecorationTouchHit(
                DecorationTouchHitKind.Furniture,
                "counter-1"));

            var result = Process(router, 1, classifier);

            Assert.That(result.Owner, Is.EqualTo(DecorationGestureOwner.None));
            Assert.That(result.OriginHit.Kind, Is.EqualTo(DecorationTouchHitKind.None));
            AssertNoCommands(result);
            Assert.That(classifier.CallCount, Is.Zero);
            Assert.That(router.PrimaryTouchId, Is.EqualTo(DecorationTouchRouter.NoTouchId));
            Assert.That(router.SecondaryTouchId, Is.EqualTo(DecorationTouchRouter.NoTouchId));
        }

        [Test]
        public void FurnitureOwner_ClassifiesPrimaryOnceAndCrossingRegionsCannotSwitchOwner()
        {
            var router = new DecorationTouchRouter(6f, 24f);
            var classifier = new RecordingClassifier(new DecorationTouchHit(
                DecorationTouchHitKind.Furniture,
                "counter-1"));

            var began = Process(router, 1, classifier, Point(11, 10f, 10f, InputTouchPhase.Began));
            var exactThreshold = Process(
                router,
                2,
                classifier,
                Point(11, 16f, 10f, InputTouchPhase.Moved, 6f, 0f));
            var dragged = Process(
                router,
                3,
                classifier,
                Point(11, 16.01f, 10f, InputTouchPhase.Moved, 0.01f, 0f));

            Assert.That(began.Owner, Is.EqualTo(DecorationGestureOwner.Furniture));
            AssertNoCommands(began);
            AssertNoCommands(exactThreshold);
            Assert.That(dragged.Owner, Is.EqualTo(DecorationGestureOwner.Furniture));
            Assert.That(dragged.FurnitureDragRequested, Is.True);
            Assert.That(dragged.FurnitureDragScreenPosition,
                Is.EqualTo(new Vector2(16.01f, 34f)).Using(Vector2ComparerWithEqualsOperator.Instance));
            Assert.That(dragged.CameraPanRequested, Is.False);
            Assert.That(classifier.CallCount, Is.EqualTo(1));
            Assert.That(classifier.LastTouchId, Is.EqualTo(11));
            Assert.That(router.IsDragging, Is.True);
        }

        [Test]
        public void CameraOwner_EmitsOnlyCameraPanAfterThreshold()
        {
            var router = new DecorationTouchRouter(3f, 20f);
            var classifier = new RecordingClassifier(new DecorationTouchHit(DecorationTouchHitKind.Scene));
            Process(router, 1, classifier, Point(5, 40f, 40f, InputTouchPhase.Began));

            var result = Process(
                router,
                2,
                classifier,
                Point(5, 44f, 41f, InputTouchPhase.Moved, 4f, 1f));

            Assert.That(result.Owner, Is.EqualTo(DecorationGestureOwner.Camera));
            Assert.That(result.CameraPanRequested, Is.True);
            Assert.That(result.CameraPanDelta,
                Is.EqualTo(new Vector2(4f, 1f)).Using(Vector2ComparerWithEqualsOperator.Instance));
            Assert.That(result.FurnitureDragRequested, Is.False);
            Assert.That(result.PinchZoomRequested, Is.False);
        }

        [Test]
        public void UiPrimary_NeverEmitsLowerLayerCommandsOrPromotesToPinch()
        {
            var router = new DecorationTouchRouter(2f, 20f);
            var classifier = new RecordingClassifier(new DecorationTouchHit(DecorationTouchHitKind.Ui));
            Process(router, 1, classifier, Point(1, 10f, 10f, InputTouchPhase.Began));

            var movedWithSecond = Process(
                router,
                2,
                classifier,
                Point(1, 30f, 30f, InputTouchPhase.Moved, 20f, 20f),
                Point(2, 90f, 90f, InputTouchPhase.Began));
            var released = Process(
                router,
                3,
                classifier,
                Point(1, 30f, 30f, InputTouchPhase.Ended),
                Point(2, 90f, 90f, InputTouchPhase.Stationary));

            Assert.That(movedWithSecond.Owner, Is.EqualTo(DecorationGestureOwner.Ui));
            AssertNoCommands(movedWithSecond);
            Assert.That(released.Owner, Is.EqualTo(DecorationGestureOwner.None));
            AssertNoCommands(released);
            Assert.That(router.IsSuppressingUntilAllTouchesUp, Is.True);
            Assert.That(classifier.CallCount, Is.EqualTo(1));
        }

        [Test]
        public void NonePrimary_SuppressesEveryTouchUntilScreenIsClearThenAllowsFreshGesture()
        {
            var router = new DecorationTouchRouter(2f, 20f);
            var classifier = new RecordingClassifier(
                default,
                new DecorationTouchHit(DecorationTouchHitKind.Scene));

            var rejected = Process(
                router,
                1,
                classifier,
                Point(1, 10f, 10f, InputTouchPhase.Began),
                Point(2, 20f, 20f, InputTouchPhase.Began));
            var stillSuppressed = Process(
                router,
                2,
                classifier,
                Point(1, 10f, 10f, InputTouchPhase.Ended),
                Point(2, 20f, 20f, InputTouchPhase.Stationary),
                Point(3, 30f, 30f, InputTouchPhase.Began));
            var cleared = Process(router, 3, classifier, Point(2, 20f, 20f, InputTouchPhase.Ended));
            var fresh = Process(router, 4, classifier, Point(4, 40f, 40f, InputTouchPhase.Began));

            Assert.That(rejected.Owner, Is.EqualTo(DecorationGestureOwner.None));
            AssertNoCommands(rejected);
            Assert.That(stillSuppressed.Owner, Is.EqualTo(DecorationGestureOwner.None));
            AssertNoCommands(stillSuppressed);
            Assert.That(cleared.Owner, Is.EqualTo(DecorationGestureOwner.None));
            Assert.That(fresh.Owner, Is.EqualTo(DecorationGestureOwner.Camera));
            Assert.That(classifier.CallCount, Is.EqualTo(2));
            Assert.That(router.IsSuppressingUntilAllTouchesUp, Is.False);
        }

        [Test]
        public void ReturningInsideThresholdAfterDrag_RemainsDragAndCannotTap()
        {
            var router = new DecorationTouchRouter(5f, 10f);
            var classifier = new RecordingClassifier(new DecorationTouchHit(
                DecorationTouchHitKind.Furniture,
                "chair-1"));
            Process(router, 1, classifier, Point(9, 0f, 0f, InputTouchPhase.Began));
            Process(router, 2, classifier, Point(9, 8f, 0f, InputTouchPhase.Moved, 8f, 0f));

            var returned = Process(router, 3, classifier, Point(9, 1f, 0f, InputTouchPhase.Moved, -7f, 0f));
            var released = Process(router, 4, classifier, Point(9, 1f, 0f, InputTouchPhase.Ended));

            Assert.That(returned.FurnitureDragRequested, Is.True);
            Assert.That(released.TapReleased, Is.False);
            Assert.That(released.Owner, Is.EqualTo(DecorationGestureOwner.None));
        }

        [Test]
        public void LongTravelPathInsidePressRadius_RemainsTapBecauseThresholdUsesMaximumStraightLineDistance()
        {
            var router = new DecorationTouchRouter(5f, 10f);
            var classifier = new RecordingClassifier(new DecorationTouchHit(DecorationTouchHitKind.Scene));
            Process(router, 1, classifier, Point(4, 0f, 0f, InputTouchPhase.Began));
            var aroundTop = Process(router, 2, classifier, Point(4, 0f, 4f, InputTouchPhase.Moved, 0f, 4f));
            var aroundLeft = Process(router, 3, classifier, Point(4, -4f, 0f, InputTouchPhase.Moved, -4f, -4f));
            var aroundBottom = Process(router, 4, classifier, Point(4, 0f, -4f, InputTouchPhase.Moved, 4f, -4f));
            var released = Process(router, 5, classifier, Point(4, 0f, 0f, InputTouchPhase.Ended, 0f, 4f));

            AssertNoCommands(aroundTop);
            AssertNoCommands(aroundLeft);
            AssertNoCommands(aroundBottom);
            Assert.That(router.IsDragging, Is.False);
            Assert.That(released.TapReleased, Is.True,
                "Path length exceeds threshold, but every straight-line press distance stays <= threshold.");
        }

        [TestCase(DecorationTouchHitKind.Furniture)]
        [TestCase(DecorationTouchHitKind.Scene)]
        public void NonUiTapRelease_EmitsExactlyOneTapWithOrigin(DecorationTouchHitKind kind)
        {
            var router = new DecorationTouchRouter(5f, 10f);
            var classifier = new RecordingClassifier(new DecorationTouchHit(kind, "table-1"));
            Process(router, 1, classifier, Point(3, 5f, 5f, InputTouchPhase.Began));

            var released = Process(router, 2, classifier, Point(3, 8f, 5f, InputTouchPhase.Ended));
            var repeated = Process(router, 3, classifier, Point(3, 8f, 5f, InputTouchPhase.Ended));

            Assert.That(released.TapReleased, Is.True);
            Assert.That(released.OriginHit.Kind, Is.EqualTo(kind));
            Assert.That(released.OriginHit.FurnitureInstanceId, Is.EqualTo("table-1"));
            Assert.That(repeated.TapReleased, Is.False);
        }

        [TestCase(DecorationTouchHitKind.Furniture)]
        [TestCase(DecorationTouchHitKind.Scene)]
        public void ReviewSafety_DirectTerminalBeyondThresholdCannotTapOrEmitDragCommand(
            DecorationTouchHitKind kind)
        {
            var router = new DecorationTouchRouter(5f, 10f);
            var classifier = new RecordingClassifier(new DecorationTouchHit(kind, "terminal-threshold"));
            Process(router, 1, classifier, Point(13, 0f, 0f, InputTouchPhase.Began));

            var released = Process(
                router,
                2,
                classifier,
                Point(13, 5.01f, 0f, InputTouchPhase.Ended, 5.01f, 0f));

            Assert.That(released.Owner, Is.EqualTo(DecorationGestureOwner.None));
            Assert.That(released.OriginHit.Kind, Is.EqualTo(kind));
            AssertNoCommands(released);
            Assert.That(router.PrimaryTouchId, Is.EqualTo(DecorationTouchRouter.NoTouchId));
            Assert.That(router.SecondaryTouchId, Is.EqualTo(DecorationTouchRouter.NoTouchId));
            Assert.That(router.IsDragging, Is.False);
            Assert.That(router.IsSuppressingUntilAllTouchesUp, Is.False);
        }

        [TestCase(DecorationTouchHitKind.Furniture)]
        [TestCase(DecorationTouchHitKind.Scene)]
        public void ReviewSafety_DirectTerminalAtExactThresholdRemainsTapEligible(
            DecorationTouchHitKind kind)
        {
            var router = new DecorationTouchRouter(5f, 10f);
            var classifier = new RecordingClassifier(new DecorationTouchHit(kind, "exact-threshold"));
            Process(router, 1, classifier, Point(14, 0f, 0f, InputTouchPhase.Began));

            var released = Process(
                router,
                2,
                classifier,
                Point(14, 5f, 0f, InputTouchPhase.Ended, 5f, 0f));

            Assert.That(released.TapReleased, Is.True);
            Assert.That(released.OriginHit.Kind, Is.EqualTo(kind));
            Assert.That(released.FurnitureDragRequested, Is.False);
            Assert.That(released.CameraPanRequested, Is.False);
            Assert.That(released.PinchZoomRequested, Is.False);
        }

        [TestCase(-24f)]
        [TestCase(0f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void ReviewSafety_NegativeNonFiniteOrDefaultFurnitureOffsetCannotMoveOutputDownward(
            float furnitureOffset)
        {
            var router = new DecorationTouchRouter(1f, furnitureOffset);
            var classifier = new RecordingClassifier(new DecorationTouchHit(
                DecorationTouchHitKind.Furniture,
                "offset-safety"));
            Process(router, 1, classifier, Point(15, 10f, 20f, InputTouchPhase.Began));

            var dragged = Process(
                router,
                2,
                classifier,
                Point(15, 12f, 20f, InputTouchPhase.Moved, 2f, 0f));

            Assert.That(dragged.FurnitureDragRequested, Is.True);
            Assert.That(dragged.FurnitureDragScreenPosition,
                Is.EqualTo(new Vector2(12f, 20f)).Using(Vector2ComparerWithEqualsOperator.Instance));
        }

        [Test]
        public void CanceledPrimary_NeverEmitsTap()
        {
            var router = new DecorationTouchRouter(5f, 10f);
            var classifier = new RecordingClassifier(new DecorationTouchHit(DecorationTouchHitKind.Scene));
            Process(router, 1, classifier, Point(7, 5f, 5f, InputTouchPhase.Began));

            var canceled = Process(router, 2, classifier, Point(7, 5f, 5f, InputTouchPhase.Canceled));

            Assert.That(canceled.Owner, Is.EqualTo(DecorationGestureOwner.None));
            AssertNoCommands(canceled);
        }

        [Test]
        public void TwoBeganInSourceOrder_EstablishPrimaryThenPinchWithoutSingleFingerCommand()
        {
            var router = new DecorationTouchRouter(2f, 10f);
            var classifier = new RecordingClassifier(new DecorationTouchHit(
                DecorationTouchHitKind.Furniture,
                "counter-first"));

            var result = Process(
                router,
                1,
                classifier,
                Point(42, 10f, 10f, InputTouchPhase.Began),
                Point(17, 30f, 10f, InputTouchPhase.Began),
                Point(99, 50f, 10f, InputTouchPhase.Began));

            Assert.That(result.Owner, Is.EqualTo(DecorationGestureOwner.Pinch));
            AssertNoCommands(result);
            Assert.That(router.PrimaryTouchId, Is.EqualTo(42));
            Assert.That(router.SecondaryTouchId, Is.EqualTo(17));
            Assert.That(router.IsDragging, Is.True);
            Assert.That(classifier.CallCount, Is.EqualTo(1));
        }

        [Test]
        public void Pinch_EmitsDistanceDeltaOnceAndNoSingleFingerCommand()
        {
            var router = BeginFurniturePinch();
            var classifier = new RecordingClassifier(new DecorationTouchHit(DecorationTouchHitKind.Ui));

            var moved = Process(
                router,
                2,
                classifier,
                Point(1, 0f, 0f, InputTouchPhase.Moved, 1f, 0f),
                Point(2, 14f, 0f, InputTouchPhase.Moved, 4f, 0f));
            var duplicate = Process(
                router,
                2,
                classifier,
                Point(1, 0f, 0f, InputTouchPhase.Moved, 1f, 0f),
                Point(2, 14f, 0f, InputTouchPhase.Moved, 4f, 0f));

            Assert.That(moved.PinchZoomRequested, Is.True);
            Assert.That(moved.PinchDistanceDelta, Is.EqualTo(4f).Within(0.0001f));
            Assert.That(moved.FurnitureDragRequested, Is.False);
            Assert.That(moved.CameraPanRequested, Is.False);
            AssertNoCommands(duplicate);
            Assert.That(classifier.CallCount, Is.Zero);
        }

        [Test]
        public void CameraPrimary_SecondBeganAlsoPromotesToPinchWithoutReclassification()
        {
            var router = new DecorationTouchRouter(2f, 10f);
            var classifier = new RecordingClassifier(new DecorationTouchHit(DecorationTouchHitKind.Scene));
            Process(router, 1, classifier, Point(6, 0f, 0f, InputTouchPhase.Began));

            var promoted = Process(
                router,
                2,
                classifier,
                Point(6, 2f, 0f, InputTouchPhase.Moved, 2f, 0f),
                Point(7, 12f, 0f, InputTouchPhase.Began));

            Assert.That(promoted.Owner, Is.EqualTo(DecorationGestureOwner.Pinch));
            AssertNoCommands(promoted);
            Assert.That(router.PrimaryTouchId, Is.EqualTo(6));
            Assert.That(router.SecondaryTouchId, Is.EqualTo(7));
            Assert.That(classifier.CallCount, Is.EqualTo(1));
        }

        [Test]
        public void P6IN007_SecondBeganAfterFurnitureDragStopsDragAndPromotesToPinch()
        {
            var router = new DecorationTouchRouter(2f, 10f);
            var classifier = new RecordingClassifier(new DecorationTouchHit(
                DecorationTouchHitKind.Furniture,
                "counter-dragging"));
            Process(router, 1, classifier, Point(1, 0f, 0f, InputTouchPhase.Began));
            var dragged = Process(
                router,
                2,
                classifier,
                Point(1, 5f, 0f, InputTouchPhase.Moved, 5f, 0f));

            var promoted = Process(
                router,
                3,
                classifier,
                Point(1, 7f, 0f, InputTouchPhase.Moved, 2f, 0f),
                Point(2, 17f, 0f, InputTouchPhase.Began));

            Assert.That(dragged.FurnitureDragRequested, Is.True,
                "Precondition: Furniture drag must already be threshold-latched.");
            Assert.That(promoted.Owner, Is.EqualTo(DecorationGestureOwner.Pinch));
            AssertNoCommands(promoted);
            Assert.That(router.IsDragging, Is.True);
            Assert.That(classifier.CallCount, Is.EqualTo(1));
        }

        [Test]
        public void FurnitureOriginHit_PersistsAcrossDragPinchRebaseAndResumedDrag()
        {
            var router = new DecorationTouchRouter(2f, 10f);
            var classifier = new RecordingClassifier(new DecorationTouchHit(
                DecorationTouchHitKind.Furniture,
                "origin-counter"));
            var began = Process(router, 1, classifier, Point(1, 0f, 0f, InputTouchPhase.Began));
            var dragged = Process(
                router,
                2,
                classifier,
                Point(1, 5f, 0f, InputTouchPhase.Moved, 5f, 0f));
            var pinched = Process(
                router,
                3,
                classifier,
                Point(1, 7f, 0f, InputTouchPhase.Moved, 2f, 0f),
                Point(2, 17f, 0f, InputTouchPhase.Began));
            var rebased = Process(
                router,
                4,
                classifier,
                Point(2, 17f, 0f, InputTouchPhase.Ended),
                Point(1, 8f, 0f, InputTouchPhase.Moved, 1f, 0f));
            var resumed = Process(
                router,
                5,
                classifier,
                Point(1, 9f, 0f, InputTouchPhase.Moved, 1f, 0f));

            foreach (var result in new[] { began, dragged, pinched, rebased, resumed })
            {
                Assert.That(result.OriginHit.Kind, Is.EqualTo(DecorationTouchHitKind.Furniture));
                Assert.That(result.OriginHit.FurnitureInstanceId, Is.EqualTo("origin-counter"));
            }

            Assert.That(resumed.FurnitureDragRequested, Is.True);
            Assert.That(classifier.CallCount, Is.EqualTo(1));
        }

        [TestCase(InputTouchPhase.Ended)]
        [TestCase(InputTouchPhase.Canceled)]
        public void SecondaryTerminal_RebasesPrimaryAndResumesDragWithoutJumpNextFrame(
            InputTouchPhase secondaryTerminalPhase)
        {
            var router = BeginFurniturePinch();
            var classifier = new RecordingClassifier();

            var secondaryEnded = Process(
                router,
                2,
                classifier,
                Point(1, 4f, 3f, InputTouchPhase.Moved, 4f, 3f),
                Point(2, 10f, 0f, secondaryTerminalPhase));
            var resumed = Process(
                router,
                3,
                classifier,
                Point(1, 6f, 3f, InputTouchPhase.Moved, 2f, 0f));

            Assert.That(secondaryEnded.Owner, Is.EqualTo(DecorationGestureOwner.Furniture));
            AssertNoCommands(secondaryEnded);
            Assert.That(resumed.FurnitureDragRequested, Is.True);
            Assert.That(resumed.FurnitureDragScreenPosition,
                Is.EqualTo(new Vector2(6f, 13f)).Using(Vector2ComparerWithEqualsOperator.Instance));
            Assert.That(router.SecondaryTouchId, Is.EqualTo(DecorationTouchRouter.NoTouchId));
        }

        [TestCase(InputTouchPhase.Ended)]
        [TestCase(InputTouchPhase.Canceled)]
        public void SecondaryTerminalWithReplacementBegan_RePinchesWithZeroCommandBaseline(
            InputTouchPhase secondaryTerminalPhase)
        {
            var router = BeginFurniturePinch();
            var classifier = new RecordingClassifier(new DecorationTouchHit(DecorationTouchHitKind.Ui));

            var replacement = Process(
                router,
                2,
                classifier,
                Point(3, 30f, 0f, InputTouchPhase.Began),
                Point(2, 10f, 0f, secondaryTerminalPhase),
                Point(1, 2f, 0f, InputTouchPhase.Moved, 2f, 0f));
            var moved = Process(
                router,
                3,
                classifier,
                Point(1, 2f, 0f, InputTouchPhase.Stationary),
                Point(3, 35f, 0f, InputTouchPhase.Moved, 5f, 0f));

            Assert.That(replacement.Owner, Is.EqualTo(DecorationGestureOwner.Pinch));
            AssertNoCommands(replacement);
            Assert.That(router.SecondaryTouchId, Is.EqualTo(3));
            Assert.That(classifier.CallCount, Is.Zero);
            Assert.That(moved.PinchZoomRequested, Is.True);
            Assert.That(moved.PinchDistanceDelta, Is.EqualTo(5f).Within(0.0001f));
        }

        [Test]
        public void PrimaryTerminalWithAnyActiveTouch_WinsAndSuppressesNewBegan()
        {
            var router = BeginFurniturePinch();
            var classifier = new RecordingClassifier(new DecorationTouchHit(DecorationTouchHitKind.Scene));

            var result = Process(
                router,
                2,
                classifier,
                Point(3, 30f, 0f, InputTouchPhase.Began),
                Point(2, 10f, 0f, InputTouchPhase.Ended),
                Point(1, 0f, 0f, InputTouchPhase.Ended));

            Assert.That(result.Owner, Is.EqualTo(DecorationGestureOwner.None));
            AssertNoCommands(result);
            Assert.That(router.IsSuppressingUntilAllTouchesUp, Is.True);
            Assert.That(classifier.CallCount, Is.Zero);
        }

        [TestCase(InputTouchPhase.Ended)]
        [TestCase(InputTouchPhase.Canceled)]
        public void PrimaryTerminalWhileSecondaryStationary_EntersSuppressionUntilEveryTouchEnds(
            InputTouchPhase primaryTerminalPhase)
        {
            var router = BeginFurniturePinch();
            var classifier = new RecordingClassifier();

            var primaryEnded = Process(
                router,
                2,
                classifier,
                Point(1, 0f, 0f, primaryTerminalPhase),
                Point(2, 10f, 0f, InputTouchPhase.Stationary));

            Assert.That(primaryEnded.Owner, Is.EqualTo(DecorationGestureOwner.None));
            AssertNoCommands(primaryEnded);
            Assert.That(router.IsSuppressingUntilAllTouchesUp, Is.True,
                "Primary terminal must enter suppression before any later terminal frame is processed.");

            var freshBeganWhileSuppressed = Process(
                router,
                3,
                classifier,
                Point(2, 10f, 0f, InputTouchPhase.Stationary),
                Point(3, 30f, 0f, InputTouchPhase.Began));
            Assert.That(freshBeganWhileSuppressed.Owner, Is.EqualTo(DecorationGestureOwner.None));
            AssertNoCommands(freshBeganWhileSuppressed);
            Assert.That(router.IsSuppressingUntilAllTouchesUp, Is.True);
            Assert.That(classifier.CallCount, Is.Zero,
                "A fresh Began during suppression must not be reclassified.");

            var allRemainingEnded = Process(
                router,
                4,
                classifier,
                Point(2, 10f, 0f, InputTouchPhase.Ended),
                Point(3, 30f, 0f, InputTouchPhase.Canceled));
            Assert.That(allRemainingEnded.Owner, Is.EqualTo(DecorationGestureOwner.None));
            Assert.That(router.IsSuppressingUntilAllTouchesUp, Is.False);
        }

        [Test]
        public void IgnoredThirdTouchLifecycle_NeverReplacesTrackedIdsOrChangesPinchOwner()
        {
            var router = BeginFurniturePinch();
            var classifier = new RecordingClassifier(new DecorationTouchHit(DecorationTouchHitKind.Ui));

            var thirdBegan = Process(
                router,
                2,
                classifier,
                Point(1, 0f, 0f, InputTouchPhase.Stationary),
                Point(2, 10f, 0f, InputTouchPhase.Stationary),
                Point(3, 20f, 0f, InputTouchPhase.Began));
            var secondaryEndedWhileThirdStationary = Process(
                router,
                3,
                classifier,
                Point(1, 0f, 0f, InputTouchPhase.Stationary),
                Point(2, 10f, 0f, InputTouchPhase.Ended),
                Point(3, 20f, 0f, InputTouchPhase.Stationary));

            Assert.That(thirdBegan.Owner, Is.EqualTo(DecorationGestureOwner.Pinch));
            AssertNoCommands(thirdBegan);
            Assert.That(secondaryEndedWhileThirdStationary.Owner, Is.EqualTo(DecorationGestureOwner.Furniture));
            AssertNoCommands(secondaryEndedWhileThirdStationary);
            Assert.That(router.PrimaryTouchId, Is.EqualTo(1));
            Assert.That(router.SecondaryTouchId, Is.EqualTo(DecorationTouchRouter.NoTouchId),
                "The ignored third Touch must not be implicitly promoted when the tracked secondary ends.");

            var thirdCanceled = Process(
                router,
                4,
                classifier,
                Point(3, 20f, 0f, InputTouchPhase.Canceled),
                Point(1, 1f, 0f, InputTouchPhase.Moved, 1f, 0f));
            Assert.That(thirdCanceled.Owner, Is.EqualTo(DecorationGestureOwner.Furniture));
            Assert.That(router.SecondaryTouchId, Is.EqualTo(DecorationTouchRouter.NoTouchId));
            Assert.That(classifier.CallCount, Is.Zero);
        }

        [TestCase(true, InputTouchPhase.Ended, InputTouchPhase.Ended)]
        [TestCase(true, InputTouchPhase.Ended, InputTouchPhase.Canceled)]
        [TestCase(true, InputTouchPhase.Canceled, InputTouchPhase.Ended)]
        [TestCase(true, InputTouchPhase.Canceled, InputTouchPhase.Canceled)]
        [TestCase(false, InputTouchPhase.Ended, InputTouchPhase.Ended)]
        [TestCase(false, InputTouchPhase.Ended, InputTouchPhase.Canceled)]
        [TestCase(false, InputTouchPhase.Canceled, InputTouchPhase.Ended)]
        [TestCase(false, InputTouchPhase.Canceled, InputTouchPhase.Canceled)]
        public void BothTrackedTerminals_AllPhaseAndSourceOrdersClearCleanly(
            bool primaryRecordFirst,
            InputTouchPhase primaryPhase,
            InputTouchPhase secondaryPhase)
        {
            var router = BeginFurniturePinch();
            var primary = Point(1, 0f, 0f, primaryPhase);
            var secondary = Point(2, 10f, 0f, secondaryPhase);

            var result = primaryRecordFirst
                ? Process(router, 2, new RecordingClassifier(), primary, secondary)
                : Process(router, 2, new RecordingClassifier(), secondary, primary);

            Assert.That(result.Owner, Is.EqualTo(DecorationGestureOwner.None));
            AssertNoCommands(result);
            Assert.That(router.IsSuppressingUntilAllTouchesUp, Is.False);
            Assert.That(router.PrimaryTouchId, Is.EqualTo(DecorationTouchRouter.NoTouchId));
            Assert.That(router.SecondaryTouchId, Is.EqualTo(DecorationTouchRouter.NoTouchId));
        }

        [Test]
        public void BothPinchTouchesTerminal_ClearOnceAndUnknownTerminalsAreNoOps()
        {
            var router = BeginFurniturePinch();
            var classifier = new RecordingClassifier();

            var unknown = Process(
                router,
                2,
                classifier,
                Point(99, 5f, 5f, InputTouchPhase.Ended),
                Point(1, 0f, 0f, InputTouchPhase.Stationary),
                Point(2, 10f, 0f, InputTouchPhase.Stationary));
            var cleared = Process(
                router,
                3,
                classifier,
                Point(2, 10f, 0f, InputTouchPhase.Canceled),
                Point(1, 0f, 0f, InputTouchPhase.Ended));
            var repeated = Process(router, 4, classifier, Point(1, 0f, 0f, InputTouchPhase.Ended));

            Assert.That(unknown.Owner, Is.EqualTo(DecorationGestureOwner.Pinch));
            AssertNoCommands(unknown);
            Assert.That(cleared.Owner, Is.EqualTo(DecorationGestureOwner.None));
            AssertNoCommands(cleared);
            Assert.That(repeated.Owner, Is.EqualTo(DecorationGestureOwner.None));
            AssertNoCommands(repeated);
            Assert.That(router.PrimaryTouchId, Is.EqualTo(DecorationTouchRouter.NoTouchId));
        }

        [Test]
        public void DuplicateAndStaleFrames_ReturnCurrentOwnerWithoutRepeatingCommands()
        {
            var router = new DecorationTouchRouter(1f, 5f);
            var classifier = new RecordingClassifier(new DecorationTouchHit(DecorationTouchHitKind.Scene));
            Process(router, 10, classifier, Point(1, 0f, 0f, InputTouchPhase.Began));
            var command = Process(
                router,
                11,
                classifier,
                Point(1, 3f, 0f, InputTouchPhase.Moved, 3f, 0f));

            var duplicate = Process(
                router,
                11,
                classifier,
                Point(1, 6f, 0f, InputTouchPhase.Moved, 3f, 0f));
            var stale = Process(
                router,
                9,
                classifier,
                Point(1, 9f, 0f, InputTouchPhase.Moved, 3f, 0f));

            Assert.That(command.CameraPanRequested, Is.True);
            Assert.That(duplicate.Owner, Is.EqualTo(DecorationGestureOwner.Camera));
            AssertNoCommands(duplicate);
            Assert.That(stale.Owner, Is.EqualTo(DecorationGestureOwner.Camera));
            AssertNoCommands(stale);
        }

        [Test]
        public void Reset_ClearsEveryGestureFieldAndFrameGuard()
        {
            var router = BeginFurniturePinch();
            router.Reset();

            Assert.That(router.Owner, Is.EqualTo(DecorationGestureOwner.None));
            Assert.That(router.PrimaryTouchId, Is.EqualTo(DecorationTouchRouter.NoTouchId));
            Assert.That(router.SecondaryTouchId, Is.EqualTo(DecorationTouchRouter.NoTouchId));
            Assert.That(router.IsDragging, Is.False);
            Assert.That(router.IsSuppressingUntilAllTouchesUp, Is.False);

            var classifier = new RecordingClassifier(new DecorationTouchHit(DecorationTouchHitKind.Scene));
            var fresh = Process(router, 1, classifier, Point(8, 1f, 1f, InputTouchPhase.Began));
            Assert.That(fresh.Owner, Is.EqualTo(DecorationGestureOwner.Camera));
        }

        [Test]
        public void Reset_FromSuppressionClearsSuppressionAndAllowsSameFrameNumberFreshGesture()
        {
            var router = new DecorationTouchRouter(2f, 10f);
            var classifier = new RecordingClassifier(
                default,
                new DecorationTouchHit(DecorationTouchHitKind.Scene));
            Process(router, 9, classifier, Point(1, 0f, 0f, InputTouchPhase.Began));
            Assert.That(router.IsSuppressingUntilAllTouchesUp, Is.True);

            router.Reset();
            var fresh = Process(router, 9, classifier, Point(2, 5f, 5f, InputTouchPhase.Began));

            Assert.That(router.IsSuppressingUntilAllTouchesUp, Is.False);
            Assert.That(fresh.Owner, Is.EqualTo(DecorationGestureOwner.Camera));
            Assert.That(router.PrimaryTouchId, Is.EqualTo(2));
        }

        [Test]
        public void TouchPointAndFrame_CountOnlyActivePhasesAndPreserveRawValues()
        {
            var points = new[]
            {
                Point(1, 2f, 3f, InputTouchPhase.Began, 4f, 5f),
                Point(2, 6f, 7f, InputTouchPhase.Ended),
                Point(3, 8f, 9f, InputTouchPhase.Canceled),
                Point(4, 10f, 11f, InputTouchPhase.None)
            };
            var frame = new DecorationTouchFrame(77, points);

            Assert.That(frame.FrameNumber, Is.EqualTo(77));
            Assert.That(frame.ActiveTouchCount, Is.EqualTo(1));
            Assert.That(frame.Touches[0].TouchId, Is.EqualTo(1));
            Assert.That(frame.Touches[0].Position,
                Is.EqualTo(new Vector2(2f, 3f)).Using(Vector2ComparerWithEqualsOperator.Instance));
            Assert.That(frame.Touches[0].Delta,
                Is.EqualTo(new Vector2(4f, 5f)).Using(Vector2ComparerWithEqualsOperator.Instance));
            Assert.That(frame.Touches[0].IsActive, Is.True);
            Assert.That(frame.Touches[1].IsTerminal, Is.True);
            Assert.That(frame.Touches[2].IsTerminal, Is.True);
            Assert.That(frame.Touches[3].IsActive, Is.False);
            Assert.That(frame.Touches[3].IsTerminal, Is.False);
        }

        [Test]
        public void SceneInteraction_NullSuppressionOwnerIsRejected()
        {
            using var fixture = new LegacySuppressionFixture();

            Assert.Throws<ArgumentNullException>(
                () => fixture.Interaction.AcquireInputSuppression(null));
        }

        [Test]
        public void SceneInteraction_NullInputStillClearsInvalidSelection()
        {
            using var fixture = new LegacySuppressionFixture();
            Assert.That(fixture.Interaction.TrySelectAt(fixture.TargetScreen), Is.True);
            Assert.That(fixture.Interaction.CurrentSelection, Is.SameAs(fixture.Selectable));
            fixture.Selectable.enabled = false;

            fixture.DisconnectInputAndRunLateUpdate();

            Assert.That(fixture.Interaction.CurrentSelection, Is.Null,
                "Task 7 suppression must not change the normal invalid-selection cleanup order.");
            Assert.That(fixture.Selectable.IsSelected, Is.False);
        }

        [Test]
        public void SceneInteraction_HeldSuppressionDoesNotClearInvalidSelection()
        {
            using var fixture = new LegacySuppressionFixture();
            Assert.That(fixture.Interaction.TrySelectAt(fixture.TargetScreen), Is.True);
            using var suppression = fixture.Interaction.AcquireInputSuppression(new object());
            fixture.Selectable.enabled = false;

            fixture.RunLateUpdate();

            Assert.That(fixture.Interaction.CurrentSelection, Is.SameAs(fixture.Selectable),
                "A held suppression lease may drain input but must not change selection.");
            Assert.That(fixture.Selectable.IsSelected, Is.True);
        }

        [UnityTest]
        public IEnumerator SceneInteraction_AcquireDuringActivePressClearsPendingOwnership()
        {
            using var fixture = new LegacySuppressionFixture();
            fixture.Queue(new CameraInputFrame(
                Vector2.zero,
                0f,
                false,
                fixture.TargetScreen,
                71,
                pointerPressed: true));
            yield return null;

            using var suppression = fixture.Interaction.AcquireInputSuppression(new object());
            yield return null;

            Assert.That(fixture.Boundary.GetOwnership(71), Is.EqualTo(UiPointerOwnership.None));
            Assert.That(fixture.Interaction.CurrentSelection, Is.Null);
        }

        [UnityTest]
        public IEnumerator SceneInteraction_HeldSuppressionDrainsReleaseWithoutSelection()
        {
            using var fixture = new LegacySuppressionFixture();
            fixture.Queue(new CameraInputFrame(
                Vector2.zero,
                0f,
                false,
                fixture.TargetScreen,
                72,
                pointerPressed: true));
            yield return null;
            using var suppression = fixture.Interaction.AcquireInputSuppression(new object());

            fixture.Queue(new CameraInputFrame(
                Vector2.zero,
                0f,
                true,
                fixture.TargetScreen,
                72,
                pointerReleased: true));
            yield return null;

            Assert.That(fixture.Interaction.CurrentSelection, Is.Null);
            Assert.That(fixture.Boundary.GetOwnership(72), Is.EqualTo(UiPointerOwnership.None));
        }

        [UnityTest]
        public IEnumerator SceneInteraction_TwoOwnersReleaseIndependently()
        {
            using var fixture = new LegacySuppressionFixture();
            var sharedOwner = new object();
            var first = fixture.Interaction.AcquireInputSuppression(sharedOwner);
            var second = fixture.Interaction.AcquireInputSuppression(sharedOwner);

            first.Dispose();
            first.Dispose();
            fixture.Queue(new CameraInputFrame(
                Vector2.zero,
                0f,
                false,
                fixture.TargetScreen,
                74,
                pointerPressed: true));
            yield return null;
            fixture.Queue(new CameraInputFrame(
                Vector2.zero,
                0f,
                true,
                fixture.TargetScreen,
                74,
                pointerReleased: true));
            yield return null;
            Assert.That(fixture.Interaction.CurrentSelection, Is.Null,
                "The second lease must still suppress after the first is disposed twice.");

            second.Dispose();
            fixture.Queue(new CameraInputFrame(
                Vector2.zero,
                0f,
                false,
                fixture.TargetScreen,
                75,
                pointerPressed: true));
            yield return null;
            fixture.Queue(new CameraInputFrame(
                Vector2.zero,
                0f,
                true,
                fixture.TargetScreen,
                75,
                pointerReleased: true));
            yield return null;
            Assert.That(fixture.Interaction.CurrentSelection, Is.SameAs(fixture.Selectable),
                "Only final release followed by a fresh press may restore selection.");
        }

        [Test]
        public void SceneInteraction_IdempotentHandleDispose()
        {
            using var fixture = new LegacySuppressionFixture();
            var handle = fixture.Interaction.AcquireInputSuppression(new object());

            Assert.DoesNotThrow(() => handle.Dispose());
            Assert.DoesNotThrow(() => handle.Dispose());
            Assert.That(fixture.Interaction.TrySelectAt(fixture.TargetScreen), Is.False);
        }

        [UnityTest]
        public IEnumerator SceneInteraction_UnsuppressOnUiReleaseWaitsForFreshPress()
        {
            using var fixture = new LegacySuppressionFixture();
            var handle = fixture.Interaction.AcquireInputSuppression(new object());
            handle.Dispose();

            fixture.Queue(new CameraInputFrame(
                Vector2.zero,
                0f,
                true,
                fixture.TargetScreen,
                73,
                pointerReleased: true));
            yield return null;

            Assert.That(fixture.Interaction.CurrentSelection, Is.Null);
            Assert.That(fixture.Interaction.TrySelectAt(fixture.TargetScreen), Is.False);
        }

        [UnityTest]
        public IEnumerator SceneInteraction_FreshMousePressRestoresNormalSelection()
        {
            using var fixture = new LegacySuppressionFixture();
            var handle = fixture.Interaction.AcquireInputSuppression(new object());
            handle.Dispose();
            fixture.Queue(new CameraInputFrame(
                Vector2.zero,
                0f,
                false,
                fixture.TargetScreen,
                -1,
                pointerPressed: true));
            yield return null;
            fixture.Queue(new CameraInputFrame(
                Vector2.zero,
                0f,
                true,
                fixture.TargetScreen,
                -1,
                pointerReleased: true));
            yield return null;

            Assert.That(fixture.Interaction.CurrentSelection, Is.SameAs(fixture.Selectable));
            Assert.That(fixture.Selectable.IsSelected, Is.True);
        }

        [Test]
        public void Task9Support_RouterModalCancelExitAndOwnerDisableResetAllIdsSuppressionFrameGuardAndEdgeStateBeforeFreshGesture()
        {
            var router = new DecorationTouchRouter(8f, 24f);
            var classifier = new RecordingClassifier(
                new DecorationTouchHit(DecorationTouchHitKind.Furniture, "counter.a"),
                new DecorationTouchHit(DecorationTouchHitKind.Scene));
            var driverObject = new GameObject("Task9Support_EdgeDriver");
            try
            {
                var driver = driverObject.AddComponent<DecorationCameraDriver>();
                driver.EdgeZonePixels = 80f;
                driver.MaxEdgeSpeedPixelsPerSecond = 600f;

                Process(router, 41, classifier,
                    Point(101, 20f, 20f, InputTouchPhase.Began));
                Process(router, 42, classifier,
                    Point(101, 40f, 20f, InputTouchPhase.Moved),
                    Point(202, 70f, 20f, InputTouchPhase.Began));
                Assert.That(router.Owner, Is.EqualTo(DecorationGestureOwner.Pinch));
                Assert.That(router.PrimaryTouchId, Is.EqualTo(101));
                Assert.That(router.SecondaryTouchId, Is.EqualTo(202));

                var edgeDelta = driver.ApplyFurnitureEdgeAutoPan(
                    DecorationGestureOwner.Furniture,
                    true,
                    new Vector2(2f, 50f),
                    new Rect(0f, 0f, 100f, 100f),
                    new Rect(0f, 0f, 100f, 100f),
                    false);
                Assert.That(edgeDelta, Is.Not.EqualTo(Vector2.zero));
                Assert.That(driver.IsEdgeAutoPanning, Is.True);

                // Modal Cancel, explicit Exit and owner disable all converge on this public cleanup pair.
                router.Reset();
                driver.StopEdgeAutoPan();
                Assert.That(router.Owner, Is.EqualTo(DecorationGestureOwner.None));
                Assert.That(router.PrimaryTouchId, Is.EqualTo(DecorationTouchRouter.NoTouchId));
                Assert.That(router.SecondaryTouchId, Is.EqualTo(DecorationTouchRouter.NoTouchId));
                Assert.That(router.IsSuppressingUntilAllTouchesUp, Is.False);
                Assert.That(driver.IsEdgeAutoPanning, Is.False);

                var fresh = Process(router, 1, classifier,
                    Point(303, 12f, 12f, InputTouchPhase.Began));
                Assert.That(fresh.Owner, Is.EqualTo(DecorationGestureOwner.Camera),
                    "Reset must clear the old frame guard so a lower frame number can start fresh.");
                Assert.That(router.PrimaryTouchId, Is.EqualTo(303));
                var terminal = Process(router, 2, classifier,
                    Point(303, 12f, 12f, InputTouchPhase.Ended));
                Assert.That(terminal.Owner, Is.EqualTo(DecorationGestureOwner.None));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(driverObject);
            }
        }

        private static DecorationTouchRouter BeginFurniturePinch()
        {
            var router = new DecorationTouchRouter(2f, 10f);
            var classifier = new RecordingClassifier(new DecorationTouchHit(
                DecorationTouchHitKind.Furniture,
                "counter-1"));
            var began = Process(
                router,
                1,
                classifier,
                Point(1, 0f, 0f, InputTouchPhase.Began),
                Point(2, 10f, 0f, InputTouchPhase.Began));
            Assert.That(began.Owner, Is.EqualTo(DecorationGestureOwner.Pinch));
            AssertNoCommands(began);
            return router;
        }

        private static DecorationTouchRoutingResult Process(
            DecorationTouchRouter router,
            int frameNumber,
            IDecorationTouchHitClassifier classifier,
            params DecorationTouchPoint[] points)
        {
            var frame = new DecorationTouchFrame(frameNumber, points);
            return router.ProcessFrame(frame, classifier);
        }

        private static DecorationTouchPoint Point(
            int id,
            float x,
            float y,
            InputTouchPhase phase,
            float deltaX = 0f,
            float deltaY = 0f)
        {
            return new DecorationTouchPoint(
                id,
                new Vector2(x, y),
                new Vector2(deltaX, deltaY),
                phase);
        }

        private static void AssertNoCommands(DecorationTouchRoutingResult result)
        {
            Assert.That(result.TapReleased, Is.False);
            Assert.That(result.FurnitureDragRequested, Is.False);
            Assert.That(result.CameraPanRequested, Is.False);
            Assert.That(result.PinchZoomRequested, Is.False);
            Assert.That(result.FurnitureDragScreenPosition,
                Is.EqualTo(Vector2.zero).Using(Vector2ComparerWithEqualsOperator.Instance));
            Assert.That(result.CameraPanDelta,
                Is.EqualTo(Vector2.zero).Using(Vector2ComparerWithEqualsOperator.Instance));
            Assert.That(result.PinchDistanceDelta, Is.Zero);
        }

        private sealed class RecordingClassifier : IDecorationTouchHitClassifier
        {
            private readonly DecorationTouchHit[] hits;

            public RecordingClassifier(params DecorationTouchHit[] configuredHits)
            {
                hits = configuredHits ?? Array.Empty<DecorationTouchHit>();
            }

            public int CallCount { get; private set; }
            public int LastTouchId { get; private set; } = int.MinValue;

            public DecorationTouchHit ClassifyBegan(int touchId, Vector2 screenPosition)
            {
                LastTouchId = touchId;
                var index = CallCount;
                CallCount++;
                return index < hits.Length ? hits[index] : default;
            }
        }

        private sealed class LegacySuppressionFixture : IDisposable
        {
            private readonly GameObject cameraObject;
            private readonly GameObject interactionObject;
            private readonly GameObject selectableObject;

            public LegacySuppressionFixture()
            {
                cameraObject = new GameObject("Task7LegacySuppressionCamera");
                Camera = cameraObject.AddComponent<UnityEngine.Camera>();
                cameraObject.transform.position = new Vector3(0f, 0f, -10f);

                interactionObject = new GameObject("Task7LegacySuppressionInteraction");
                Input = interactionObject.AddComponent<QueuedCameraInputSource>();
                Interaction = interactionObject.AddComponent<SceneInteractionController>();
                Boundary = new UiPointerBoundary();
                Interaction.Configure(Camera, Input, Boundary);

                selectableObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                selectableObject.name = "Task7LegacySuppressionSelectable";
                selectableObject.transform.position = Vector3.zero;
                Selectable = selectableObject.AddComponent<Task4Selectable>();
                Physics.SyncTransforms();
                TargetScreen = Camera.WorldToScreenPoint(selectableObject.transform.position);
            }

            public UnityEngine.Camera Camera { get; }
            public QueuedCameraInputSource Input { get; }
            public SceneInteractionController Interaction { get; }
            public UiPointerBoundary Boundary { get; }
            public Task4Selectable Selectable { get; }
            public Vector2 TargetScreen { get; }

            public void Queue(CameraInputFrame frame)
            {
                Input.NextFrame = frame;
            }

            public void DisconnectInputAndRunLateUpdate()
            {
                var inputField = typeof(SceneInteractionController).GetField(
                    "inputSource",
                    System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.NonPublic);
                Assert.That(inputField, Is.Not.Null);
                inputField.SetValue(Interaction, null);
                RunLateUpdate();
            }

            public void RunLateUpdate()
            {
                var lateUpdate = typeof(SceneInteractionController).GetMethod(
                    "LateUpdate",
                    System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.NonPublic);
                Assert.That(lateUpdate, Is.Not.Null);
                lateUpdate.Invoke(Interaction, null);
            }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(selectableObject);
                UnityEngine.Object.DestroyImmediate(interactionObject);
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

        private sealed class QueuedCameraInputSource : MonoBehaviour, ICameraInputSource
        {
            public CameraInputFrame NextFrame { get; set; }

            public CameraInputFrame ReadFrame()
            {
                var frame = NextFrame;
                NextFrame = default;
                return frame;
            }
        }
    }

    public sealed class Phase6DecorationTouchCameraDriverTests
    {
        [TestCase(1f, 50f, 1f, 0f)]
        [TestCase(99f, 50f, -1f, 0f)]
        [TestCase(50f, 1f, 0f, 1f)]
        [TestCase(50f, 99f, 0f, -1f)]
        public void EdgeMath_FourEdgesProduceApprovedSyntheticDragDirection(
            float x,
            float y,
            float expectedXSign,
            float expectedYSign)
        {
            var delta = DecorationCameraDriver.CalculateEdgeAutoPanScreenDelta(
                new Rect(0f, 0f, 100f, 100f),
                new Rect(0f, 0f, 100f, 100f),
                new Vector2(x, y),
                20f,
                AnimationCurve.Linear(0f, 0f, 1f, 1f),
                100f,
                1f);

            AssertAxisDirection(delta.x, expectedXSign);
            AssertAxisDirection(delta.y, expectedYSign);
        }

        [Test]
        public void EdgeMath_UsesViewportSafeAreaIntersectionAndRejectsOutsideOrEmptyRects()
        {
            var curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
            var cameraRect = new Rect(0f, 0f, 200f, 200f);
            var safeArea = new Rect(20f, 30f, 160f, 140f);

            var safeLeft = DecorationCameraDriver.CalculateEdgeAutoPanScreenDelta(
                cameraRect,
                safeArea,
                new Vector2(21f, 100f),
                20f,
                curve,
                100f,
                1f);
            var outside = DecorationCameraDriver.CalculateEdgeAutoPanScreenDelta(
                cameraRect,
                safeArea,
                new Vector2(10f, 100f),
                20f,
                curve,
                100f,
                1f);
            var empty = DecorationCameraDriver.CalculateEdgeAutoPanScreenDelta(
                new Rect(0f, 0f, 10f, 10f),
                new Rect(20f, 20f, 10f, 10f),
                new Vector2(5f, 5f),
                20f,
                curve,
                100f,
                1f);
            var safeAreaExtendsPastViewport = DecorationCameraDriver.CalculateEdgeAutoPanScreenDelta(
                new Rect(20f, 30f, 60f, 40f),
                new Rect(-100f, -100f, 400f, 400f),
                new Vector2(21f, 50f),
                10f,
                curve,
                100f,
                1f);

            Assert.That(safeLeft.x, Is.GreaterThan(0f));
            Assert.That(outside, Is.EqualTo(Vector2.zero).Using(Vector2ComparerWithEqualsOperator.Instance));
            Assert.That(empty, Is.EqualTo(Vector2.zero).Using(Vector2ComparerWithEqualsOperator.Instance));
            Assert.That(safeAreaExtendsPastViewport.x, Is.GreaterThan(0f),
                "The Camera viewport, not the larger Safe Area, must own this partial intersection edge.");
        }

        [TestCase(0f, 0f, 1f, 1f)]
        [TestCase(100f, 0f, -1f, 1f)]
        [TestCase(0f, 100f, 1f, -1f)]
        [TestCase(100f, 100f, -1f, -1f)]
        public void EdgeMath_AllCornersUseBothAxesAndCapFinalMagnitude(
            float x,
            float y,
            float expectedXSign,
            float expectedYSign)
        {
            var rect = new Rect(0f, 0f, 100f, 100f);
            var delta = DecorationCameraDriver.CalculateEdgeAutoPanScreenDelta(
                rect,
                rect,
                new Vector2(x, y),
                20f,
                AnimationCurve.Linear(0f, 0f, 1f, 1f),
                40f,
                0.5f);

            AssertAxisDirection(delta.x, expectedXSign);
            AssertAxisDirection(delta.y, expectedYSign);
            Assert.That(delta.magnitude, Is.EqualTo(20f).Within(0.0001f));
        }

        [Test]
        public void EdgeMath_SampledCurveResponseIsOrderedAndOutputAndCornerMagnitudeAreCapped()
        {
            var curve = new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.5f, 0.25f),
                new Keyframe(1f, 2f));
            var rect = new Rect(0f, 0f, 100f, 100f);
            var outer = DecorationCameraDriver.CalculateEdgeAutoPanScreenDelta(
                rect, rect, new Vector2(1f, 50f), 20f, curve, 50f, 1f);
            var inner = DecorationCameraDriver.CalculateEdgeAutoPanScreenDelta(
                rect, rect, new Vector2(15f, 50f), 20f, curve, 50f, 1f);
            var corner = DecorationCameraDriver.CalculateEdgeAutoPanScreenDelta(
                rect, rect, new Vector2(0f, 0f), 20f, curve, 50f, 0.5f);

            Assert.That(outer.magnitude, Is.GreaterThan(inner.magnitude));
            Assert.That(outer.magnitude, Is.LessThanOrEqualTo(50f + 0.0001f));
            Assert.That(corner.magnitude, Is.EqualTo(25f).Within(0.0001f));
            Assert.That(corner.x, Is.GreaterThan(0f));
            Assert.That(corner.y, Is.GreaterThan(0f));
        }

        [Test]
        public void EdgeMath_EqualElapsedTimeProducesEqualTotalMovement()
        {
            var rect = new Rect(0f, 0f, 100f, 100f);
            var curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
            var oneStep = DecorationCameraDriver.CalculateEdgeAutoPanScreenDelta(
                rect, rect, new Vector2(2f, 50f), 20f, curve, 120f, 1f);
            var quarterStep = DecorationCameraDriver.CalculateEdgeAutoPanScreenDelta(
                rect, rect, new Vector2(2f, 50f), 20f, curve, 120f, 0.25f);

            Assert.That(quarterStep * 4f,
                Is.EqualTo(oneStep).Using(Vector2ComparerWithEqualsOperator.Instance));
        }

        [Test]
        public void Driver_AllFourEdgesMapToRealCameraRightAndForwardAxesAndPreserveBounds()
        {
            using var fixture = new CameraFixture();
            fixture.Settings.PanSpeed = 1f;
            fixture.Settings.PositionMin = new Vector2(-100f, -100f);
            fixture.Settings.PositionMax = new Vector2(100f, 100f);
            fixture.CameraObject.transform.rotation = Quaternion.Euler(35.264f, 45f, 0f);
            fixture.Driver.EdgeZonePixels = 20f;
            fixture.Driver.MaxEdgeSpeedPixelsPerSecond = 100f;
            fixture.Driver.NormalizedSpeedCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
            var flatRight = Vector3.ProjectOnPlane(fixture.CameraObject.transform.right, Vector3.up).normalized;
            var flatForward = Vector3.ProjectOnPlane(fixture.CameraObject.transform.forward, Vector3.up).normalized;
            var rect = new Rect(0f, 0f, 100f, 100f);

            AssertRealCameraAxis(fixture, new Vector2(1f, 50f), rect, flatRight, -1f);
            AssertRealCameraAxis(fixture, new Vector2(99f, 50f), rect, flatRight, 1f);
            AssertRealCameraAxis(fixture, new Vector2(50f, 1f), rect, flatForward, -1f);
            AssertRealCameraAxis(fixture, new Vector2(50f, 99f), rect, flatForward, 1f);

            fixture.Settings.PositionMin = new Vector2(-2f, -2f);
            fixture.Settings.PositionMax = new Vector2(2f, 2f);
            fixture.Driver.ApplyScenePan(new Vector2(-10000f, -10000f));
            Assert.That(fixture.CameraObject.transform.position.x, Is.InRange(-2f, 2f));
            Assert.That(fixture.CameraObject.transform.position.z, Is.InRange(-2f, 2f));
        }

        [Test]
        public void Driver_DelegatesPinchAndPreservesExistingZoomBounds()
        {
            using var fixture = new CameraFixture();
            fixture.Settings.ZoomSpeed = 2f;
            fixture.Settings.MinOrthographicSize = 4f;
            fixture.Settings.MaxOrthographicSize = 8f;
            fixture.Camera.orthographicSize = 6f;

            fixture.Driver.ApplyPinchZoom(100f);
            fixture.Driver.ApplyPinchZoom(100f);
            fixture.Driver.ApplyPinchZoom(100f);
            Assert.That(fixture.Camera.orthographicSize, Is.EqualTo(4f));

            fixture.Driver.ApplyPinchZoom(-100f);
            fixture.Driver.ApplyPinchZoom(-100f);
            fixture.Driver.ApplyPinchZoom(-100f);
            Assert.That(fixture.Camera.orthographicSize, Is.EqualTo(8f));
        }

        [TestCase(float.NaN, 0f)]
        [TestCase(float.PositiveInfinity, 0f)]
        [TestCase(float.NegativeInfinity, 0f)]
        [TestCase(0f, float.NaN)]
        [TestCase(0f, float.PositiveInfinity)]
        [TestCase(0f, float.NegativeInfinity)]
        public void ReviewSafety_NonFiniteScenePanIngressCannotCorruptRealCamera(float x, float y)
        {
            using var fixture = new CameraFixture();
            var before = fixture.CameraObject.transform.position;

            fixture.Driver.ApplyScenePan(new Vector2(x, y));

            Assert.That(fixture.CameraObject.transform.position,
                Is.EqualTo(before).Using(Vector3ComparerWithEqualsOperator.Instance));
            Assert.That(IsFinite(fixture.CameraObject.transform.position), Is.True);
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void ReviewSafety_NonFinitePinchIngressCannotCorruptRealCamera(float pinchDelta)
        {
            using var fixture = new CameraFixture();
            var before = fixture.Camera.orthographicSize;

            fixture.Driver.ApplyPinchZoom(pinchDelta);

            Assert.That(fixture.Camera.orthographicSize, Is.EqualTo(before));
            Assert.That(float.IsNaN(fixture.Camera.orthographicSize), Is.False);
            Assert.That(float.IsInfinity(fixture.Camera.orthographicSize), Is.False);
        }

        [UnityTest]
        public IEnumerator Driver_UsesUnscaledTimeAndStopsImmediatelyForEveryIneligibleCondition()
        {
            var originalTimeScale = Time.timeScale;
            CameraFixture fixture = null;
            try
            {
                fixture = new CameraFixture();
                fixture.Driver.EdgeZonePixels = 20f;
                fixture.Driver.MaxEdgeSpeedPixelsPerSecond = 100f;
                fixture.Driver.NormalizedSpeedCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
                var rect = new Rect(0f, 0f, 100f, 100f);
                Time.timeScale = 0f;
                yield return null;

                var pausedDelta = fixture.Driver.ApplyFurnitureEdgeAutoPan(
                    DecorationGestureOwner.Furniture,
                    true,
                    new Vector2(1f, 50f),
                    rect,
                    rect,
                    false);
                Assert.That(pausedDelta.magnitude, Is.GreaterThan(0f));
                Assert.That(fixture.Driver.IsEdgeAutoPanning, Is.True);

                var beforeExplicitStop = fixture.CameraObject.transform.position;
                fixture.Driver.StopEdgeAutoPan();
                Assert.That(fixture.Driver.IsEdgeAutoPanning, Is.False);
                yield return null;
                Assert.That(fixture.CameraObject.transform.position,
                    Is.EqualTo(beforeExplicitStop).Using(Vector3ComparerWithEqualsOperator.Instance),
                    "Stop must clear current state; no stored velocity may move Camera on a later frame.");

                AssertStoppedWithoutMoving(fixture, () => fixture.Driver.ApplyFurnitureEdgeAutoPan(
                    DecorationGestureOwner.Camera, true, new Vector2(1f, 50f), rect, rect, false));
                AssertStoppedWithoutMoving(fixture, () => fixture.Driver.ApplyFurnitureEdgeAutoPan(
                    DecorationGestureOwner.Furniture, false, new Vector2(1f, 50f), rect, rect, false));
                AssertStoppedWithoutMoving(fixture, () => fixture.Driver.ApplyFurnitureEdgeAutoPan(
                    DecorationGestureOwner.Furniture, true, new Vector2(1f, 50f), rect, rect, true));
                AssertStoppedWithoutMoving(fixture, () => fixture.Driver.ApplyFurnitureEdgeAutoPan(
                    DecorationGestureOwner.Furniture, true, new Vector2(50f, 50f), rect, rect, false));

                fixture.Driver.enabled = true;
                fixture.Driver.ApplyFurnitureEdgeAutoPan(
                    DecorationGestureOwner.Furniture,
                    true,
                    new Vector2(1f, 50f),
                    rect,
                    rect,
                    false);
                Assert.That(fixture.Driver.IsEdgeAutoPanning, Is.True);
                fixture.Driver.enabled = false;
                Assert.That(fixture.Driver.IsEdgeAutoPanning, Is.False);
            }
            finally
            {
                Time.timeScale = originalTimeScale;
                fixture?.Dispose();
            }
        }

        [Test]
        public void Driver_RejectsNonFiniteOrNegativeTuningValues()
        {
            using var fixture = new CameraFixture();
            fixture.Driver.EdgeZonePixels = float.NaN;
            fixture.Driver.MaxEdgeSpeedPixelsPerSecond = float.PositiveInfinity;

            Assert.That(fixture.Driver.EdgeZonePixels, Is.Zero);
            Assert.That(fixture.Driver.MaxEdgeSpeedPixelsPerSecond, Is.Zero);
        }

        [Test]
        public void EdgeMath_EachInvalidInputIndependentlyProducesZero()
        {
            var rect = new Rect(0f, 0f, 100f, 100f);
            var pointer = new Vector2(1f, 50f);
            var curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

            AssertZeroEdgeDelta(new Rect(0f, 0f, 0f, 100f), rect, pointer, 20f, curve, 100f, 1f);
            AssertZeroEdgeDelta(rect, new Rect(0f, 0f, 100f, 0f), pointer, 20f, curve, 100f, 1f);
            AssertZeroEdgeDelta(new Rect(float.NaN, 0f, 100f, 100f), rect, pointer, 20f, curve, 100f, 1f);
            AssertZeroEdgeDelta(new Rect(0f, float.PositiveInfinity, 100f, 100f), rect, pointer, 20f, curve, 100f, 1f);
            AssertZeroEdgeDelta(new Rect(0f, 0f, float.NegativeInfinity, 100f), rect, pointer, 20f, curve, 100f, 1f);
            AssertZeroEdgeDelta(new Rect(0f, 0f, 100f, float.NaN), rect, pointer, 20f, curve, 100f, 1f);
            AssertZeroEdgeDelta(rect, new Rect(float.NegativeInfinity, 0f, 100f, 100f), pointer, 20f, curve, 100f, 1f);
            AssertZeroEdgeDelta(rect, new Rect(0f, float.NaN, 100f, 100f), pointer, 20f, curve, 100f, 1f);
            AssertZeroEdgeDelta(rect, new Rect(0f, 0f, float.PositiveInfinity, 100f), pointer, 20f, curve, 100f, 1f);
            AssertZeroEdgeDelta(rect, new Rect(0f, 0f, 100f, float.NegativeInfinity), pointer, 20f, curve, 100f, 1f);
            AssertZeroEdgeDelta(rect, rect, new Vector2(float.NaN, 50f), 20f, curve, 100f, 1f);
            AssertZeroEdgeDelta(rect, rect, new Vector2(float.PositiveInfinity, 50f), 20f, curve, 100f, 1f);
            AssertZeroEdgeDelta(rect, rect, new Vector2(float.NegativeInfinity, 50f), 20f, curve, 100f, 1f);
            AssertZeroEdgeDelta(rect, rect, pointer, -1f, curve, 100f, 1f);
            AssertZeroEdgeDelta(rect, rect, pointer, float.PositiveInfinity, curve, 100f, 1f);
            AssertZeroEdgeDelta(rect, rect, pointer, float.NegativeInfinity, curve, 100f, 1f);
            AssertZeroEdgeDelta(rect, rect, pointer, 20f, null, 100f, 1f);
            AssertZeroEdgeDelta(rect, rect, pointer, 20f, curve, -1f, 1f);
            AssertZeroEdgeDelta(rect, rect, pointer, 20f, curve, float.PositiveInfinity, 1f);
            AssertZeroEdgeDelta(rect, rect, pointer, 20f, curve, float.NegativeInfinity, 1f);
            AssertZeroEdgeDelta(rect, rect, pointer, 20f, curve, 100f, 0f);
            AssertZeroEdgeDelta(rect, rect, pointer, 20f, curve, 100f, float.NaN);
            AssertZeroEdgeDelta(rect, rect, pointer, 20f, curve, 100f, float.NegativeInfinity);
        }

        private static void AssertStopped(Vector2 delta, DecorationCameraDriver driver)
        {
            Assert.That(delta, Is.EqualTo(Vector2.zero).Using(Vector2ComparerWithEqualsOperator.Instance));
            Assert.That(driver.IsEdgeAutoPanning, Is.False);
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x)
                && !float.IsInfinity(value.x)
                && !float.IsNaN(value.y)
                && !float.IsInfinity(value.y)
                && !float.IsNaN(value.z)
                && !float.IsInfinity(value.z);
        }

        private static void AssertStoppedWithoutMoving(CameraFixture fixture, Func<Vector2> operation)
        {
            var before = fixture.CameraObject.transform.position;
            AssertStopped(operation(), fixture.Driver);
            Assert.That(fixture.CameraObject.transform.position,
                Is.EqualTo(before).Using(Vector3ComparerWithEqualsOperator.Instance));
        }

        private static void AssertAxisDirection(float actual, float expectedSign)
        {
            if (expectedSign == 0f)
            {
                Assert.That(actual, Is.Zero);
                return;
            }

            Assert.That(Mathf.Sign(actual), Is.EqualTo(expectedSign));
        }

        private static void AssertRealCameraAxis(
            CameraFixture fixture,
            Vector2 pointer,
            Rect rect,
            Vector3 worldAxis,
            float expectedSign)
        {
            fixture.CameraObject.transform.position = new Vector3(0f, 10f, 0f);
            var before = fixture.CameraObject.transform.position;
            var applied = fixture.Driver.ApplyFurnitureEdgeAutoPan(
                DecorationGestureOwner.Furniture,
                true,
                pointer,
                rect,
                rect,
                false);
            var movement = fixture.CameraObject.transform.position - before;

            Assert.That(applied, Is.Not.EqualTo(Vector2.zero));
            AssertAxisDirection(Vector3.Dot(movement, worldAxis), expectedSign);
        }

        private static void AssertZeroEdgeDelta(
            Rect cameraRect,
            Rect safeArea,
            Vector2 pointer,
            float zone,
            AnimationCurve curve,
            float speed,
            float deltaTime)
        {
            var delta = DecorationCameraDriver.CalculateEdgeAutoPanScreenDelta(
                cameraRect,
                safeArea,
                pointer,
                zone,
                curve,
                speed,
                deltaTime);
            Assert.That(delta, Is.EqualTo(Vector2.zero).Using(Vector2ComparerWithEqualsOperator.Instance));
        }

        private sealed class CameraFixture : IDisposable
        {
            private readonly GameObject driverObject;
            private readonly GameObject inputObject;

            public CameraFixture()
            {
                CameraObject = new GameObject("Phase6DecorationTouchCamera");
                Camera = CameraObject.AddComponent<UnityEngine.Camera>();
                Camera.orthographic = true;
                Camera.orthographicSize = 6f;
                CameraObject.transform.position = new Vector3(0f, 10f, 0f);
                Settings = ScriptableObject.CreateInstance<CameraSettings>();
                Settings.PositionMin = new Vector2(-100f, -100f);
                Settings.PositionMax = new Vector2(100f, 100f);
                Settings.MinOrthographicSize = 1f;
                Settings.MaxOrthographicSize = 20f;
                inputObject = new GameObject("Phase6DecorationTouchSilentCameraInput");
                var input = inputObject.AddComponent<SilentCameraInputSource>();
                Controller = CameraObject.AddComponent<CafeCameraController>();
                Controller.Configure(Camera, Settings, input);
                driverObject = new GameObject("Phase6DecorationCameraDriver");
                Driver = driverObject.AddComponent<DecorationCameraDriver>();
                Driver.Configure(Controller);
            }

            public GameObject CameraObject { get; }
            public UnityEngine.Camera Camera { get; }
            public CameraSettings Settings { get; }
            public CafeCameraController Controller { get; }
            public DecorationCameraDriver Driver { get; }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(driverObject);
                UnityEngine.Object.DestroyImmediate(inputObject);
                UnityEngine.Object.DestroyImmediate(Settings);
                UnityEngine.Object.DestroyImmediate(CameraObject);
            }
        }

        private sealed class SilentCameraInputSource : MonoBehaviour, ICameraInputSource
        {
            public CameraInputFrame ReadFrame() => default;
        }
    }

    public sealed class Phase6DecorationTouchSourceTests : InputTestFixture
    {
        [UnityTest]
        public IEnumerator MouseSource_EmitsOneSemanticPointerAndReadsWheelWithoutTouchIds()
        {
            Mouse mouse = null;
            GameObject sourceObject = null;
            try
            {
                mouse = InputSystem.AddDevice<Mouse>();
                sourceObject = new GameObject("Phase6DecorationMouseSource");
                var source = sourceObject.AddComponent<MouseDecorationInputSource>();

                Set(mouse.position, new Vector2(100f, 120f));
                yield return null;
                Press(mouse.leftButton);
                yield return null;
                var began = Capture((IDecorationTouchSource)source);
                Assert.That(began.Touches, Has.Length.EqualTo(1));
                Assert.That(began.Touches[0].TouchId,
                    Is.EqualTo(MouseDecorationInputSource.PointerId));
                Assert.That(began.Touches[0].Position,
                    Is.EqualTo(new Vector2(100f, 120f))
                        .Using(Vector2ComparerWithEqualsOperator.Instance));
                Assert.That(began.Touches[0].Phase, Is.EqualTo(InputTouchPhase.Began));

                yield return null;
                Set(mouse.position, new Vector2(112f, 129f));
                yield return null;
                var moved = Capture((IDecorationTouchSource)source);
                Assert.That(moved.Touches[0].Delta,
                    Is.EqualTo(new Vector2(12f, 9f))
                        .Using(Vector2ComparerWithEqualsOperator.Instance));
                Assert.That(moved.Touches[0].Phase, Is.EqualTo(InputTouchPhase.Moved));

                yield return null;
                Release(mouse.leftButton);
                yield return null;
                var ended = Capture((IDecorationTouchSource)source);
                Assert.That(ended.Touches[0].Phase, Is.EqualTo(InputTouchPhase.Ended));
                Assert.That(source.HasActivePointer, Is.False);

                yield return null;
                Set(mouse.scroll, new Vector2(0f, 3f));
                yield return null;
                Assert.That(source.ReadScrollDelta(), Is.EqualTo(3f));
                Assert.That(source.ReadScrollDelta(), Is.Zero,
                    "Wheel delta is consumed exactly once per frame.");
            }
            finally
            {
                if (sourceObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(sourceObject);
                }
                if (mouse != null && mouse.added)
                {
                    InputSystem.RemoveDevice(mouse);
                }
            }
        }

        [UnityTest]
        public IEnumerator MouseSource_ResetCancelsCachedGestureAndDisableReturnsEmpty()
        {
            Mouse mouse = null;
            GameObject sourceObject = null;
            try
            {
                mouse = InputSystem.AddDevice<Mouse>();
                sourceObject = new GameObject("Phase6DecorationMouseResetSource");
                var source = sourceObject.AddComponent<MouseDecorationInputSource>();
                Set(mouse.position, new Vector2(40f, 50f));
                yield return null;
                Press(mouse.leftButton);
                yield return null;
                Assert.That(Capture((IDecorationTouchSource)source).ActiveTouchCount,
                    Is.EqualTo(1));

                source.Reset();
                Assert.That(source.HasActivePointer, Is.False);
                yield return null;
                Assert.That(Capture((IDecorationTouchSource)source).Touches, Is.Empty);

                source.enabled = false;
                Assert.That(Capture((IDecorationTouchSource)source).Touches, Is.Empty);
            }
            finally
            {
                if (sourceObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(sourceObject);
                }
                if (mouse != null && mouse.added)
                {
                    InputSystem.RemoveDevice(mouse);
                }
            }
        }

        [UnityTest]
        public IEnumerator MouseSource_LosingApplicationFocusEmitsCanceledThenAllowsFreshGesture()
        {
            Mouse mouse = null;
            GameObject sourceObject = null;
            try
            {
                mouse = InputSystem.AddDevice<Mouse>();
                sourceObject = new GameObject("Phase6DecorationMouseFocusSource");
                var source = sourceObject.AddComponent<MouseDecorationInputSource>();
                Set(mouse.position, new Vector2(60f, 70f));
                yield return null;
                Press(mouse.leftButton);
                yield return null;
                Assert.That(Capture((IDecorationTouchSource)source).ActiveTouchCount,
                    Is.EqualTo(1));

                var callback = typeof(MouseDecorationInputSource).GetMethod(
                    "OnApplicationFocus",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(callback, Is.Not.Null,
                    "Mouse source must clear ownership when the application loses focus.");
                callback.Invoke(source, new object[] { false });

                Assert.That(source.HasActivePointer, Is.False);
                var canceled = Capture((IDecorationTouchSource)source);
                Assert.That(canceled.Touches, Has.Length.EqualTo(1));
                Assert.That(canceled.Touches[0].TouchId,
                    Is.EqualTo(MouseDecorationInputSource.PointerId));
                Assert.That(canceled.Touches[0].Phase, Is.EqualTo(InputTouchPhase.Canceled));

                Release(mouse.leftButton);
                yield return null;
                Assert.That(Capture((IDecorationTouchSource)source).Touches, Is.Empty,
                    "The physical release only clears focus-loss suppression.");
                Press(mouse.leftButton);
                yield return null;
                Assert.That(Capture((IDecorationTouchSource)source).Touches.Single().Phase,
                    Is.EqualTo(InputTouchPhase.Began));
            }
            finally
            {
                if (sourceObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(sourceObject);
                }
                if (mouse != null && mouse.added)
                {
                    InputSystem.RemoveDevice(mouse);
                }
            }
        }

        [UnityTest]
        public IEnumerator MouseSource_DisableEmitsCanceledThenAllowsFreshGestureAfterEnable()
        {
            Mouse mouse = null;
            GameObject sourceObject = null;
            try
            {
                mouse = InputSystem.AddDevice<Mouse>();
                sourceObject = new GameObject("Phase6DecorationMouseDisableSource");
                var source = sourceObject.AddComponent<MouseDecorationInputSource>();
                Set(mouse.position, new Vector2(80f, 90f));
                yield return null;
                Press(mouse.leftButton);
                yield return null;
                Assert.That(Capture((IDecorationTouchSource)source).Touches.Single().Phase,
                    Is.EqualTo(InputTouchPhase.Began));

                source.enabled = false;
                Assert.That(source.HasActivePointer, Is.False);
                var canceled = Capture((IDecorationTouchSource)source);
                Assert.That(canceled.Touches, Has.Length.EqualTo(1));
                Assert.That(canceled.Touches[0].TouchId,
                    Is.EqualTo(MouseDecorationInputSource.PointerId));
                Assert.That(canceled.Touches[0].Phase, Is.EqualTo(InputTouchPhase.Canceled));

                Release(mouse.leftButton);
                yield return null;
                source.enabled = true;
                yield return null;
                Press(mouse.leftButton);
                yield return null;
                Assert.That(Capture((IDecorationTouchSource)source).Touches.Single().Phase,
                    Is.EqualTo(InputTouchPhase.Began));
            }
            finally
            {
                if (sourceObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(sourceObject);
                }
                if (mouse != null && mouse.added)
                {
                    InputSystem.RemoveDevice(mouse);
                }
            }
        }

        [UnityTest]
        public IEnumerator Source_CachesSameFrameAndPreservesRawIdPositionAccumulatedDeltaAndPhase()
        {
            Touchscreen device = null;
            GameObject sourceObject = null;
            InputSystemDecorationTouchSource source = null;
            try
            {
                device = InputSystem.AddDevice<Touchscreen>();
                sourceObject = new GameObject("Phase6DecorationTouchSource");
                source = sourceObject.AddComponent<InputSystemDecorationTouchSource>();
                BeginTouch(37, new Vector2(10f, 20f), screen: device);
                AssertSingle(source, 37, new Vector2(10f, 20f), Vector2.zero, InputTouchPhase.Began);

                var beforeMutation = Capture(source);
                MoveTouch(37, new Vector2(16f, 29f), new Vector2(6f, 9f), screen: device);
                var afterSameFrameMutation = Capture(source);
                AssertSnapshotsEqual(beforeMutation, afterSameFrameMutation,
                    "A second read must return the cached snapshot even after another InputSystem.Update in the same Time.frameCount.");
                yield return null;

                AssertSingle(source, 37, new Vector2(16f, 29f), Vector2.zero, InputTouchPhase.Stationary);
                yield return null;

                MoveTouch(
                    37,
                    new Vector2(18f, 31f),
                    new Vector2(2f, 2f),
                    queueEventOnly: true,
                    screen: device);
                MoveTouch(
                    37,
                    new Vector2(21f, 35f),
                    new Vector2(3f, 4f),
                    queueEventOnly: true,
                    screen: device);
                InputSystem.Update();
                AssertSingle(source, 37, new Vector2(21f, 35f), new Vector2(5f, 6f), InputTouchPhase.Moved);
            }
            finally
            {
                if (sourceObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(sourceObject);
                }

                if (device != null && device.added)
                {
                    InputSystem.RemoveDevice(device);
                }
            }
        }

        [UnityTest]
        public IEnumerator Source_ShortBeganThenEndedSurfacesBothWithoutLossAcrossPollingFrames()
        {
            Touchscreen device = null;
            GameObject sourceObject = null;
            try
            {
                device = InputSystem.AddDevice<Touchscreen>();
                sourceObject = new GameObject("Phase6DecorationShortEndedSource");
                var source = sourceObject.AddComponent<InputSystemDecorationTouchSource>();
                BeginTouch(4, new Vector2(10f, 20f), queueEventOnly: true, screen: device);
                EndTouch(4, new Vector2(12f, 23f), new Vector2(2f, 3f), queueEventOnly: true, screen: device);
                InputSystem.Update();
                AssertSingle(source, 4, new Vector2(10f, 20f), Vector2.zero, InputTouchPhase.Began);

                yield return null;
                AssertSingle(source, 4, new Vector2(12f, 23f), new Vector2(2f, 3f), InputTouchPhase.Ended);
            }
            finally
            {
                if (sourceObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(sourceObject);
                }

                if (device != null && device.added)
                {
                    InputSystem.RemoveDevice(device);
                }
            }
        }

        [UnityTest]
        public IEnumerator Source_ShortBeganThenCanceledSurfacesBothWithoutLossAcrossPollingFrames()
        {
            Touchscreen device = null;
            GameObject sourceObject = null;
            try
            {
                device = InputSystem.AddDevice<Touchscreen>();
                sourceObject = new GameObject("Phase6DecorationShortCanceledSource");
                var source = sourceObject.AddComponent<InputSystemDecorationTouchSource>();
                BeginTouch(8, new Vector2(30f, 40f), queueEventOnly: true, screen: device);
                CancelTouch(8, new Vector2(33f, 44f), new Vector2(3f, 4f), queueEventOnly: true, screen: device);
                InputSystem.Update();
                AssertSingle(source, 8, new Vector2(30f, 40f), Vector2.zero, InputTouchPhase.Began);

                yield return null;
                AssertSingle(source, 8, new Vector2(33f, 44f), new Vector2(3f, 4f), InputTouchPhase.Canceled);
            }
            finally
            {
                if (sourceObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(sourceObject);
                }

                if (device != null && device.added)
                {
                    InputSystem.RemoveDevice(device);
                }
            }
        }

        [UnityTest]
        public IEnumerator Source_TwoTouchIdsRemainInEnhancedTouchSourceOrder()
        {
            Touchscreen device = null;
            GameObject sourceObject = null;
            try
            {
                device = InputSystem.AddDevice<Touchscreen>();
                sourceObject = new GameObject("Phase6DecorationTwoTouchSource");
                var source = sourceObject.AddComponent<InputSystemDecorationTouchSource>();
                BeginTouch(42, new Vector2(10f, 10f), queueEventOnly: true, screen: device);
                BeginTouch(17, new Vector2(20f, 20f), queueEventOnly: true, screen: device);
                InputSystem.Update();

                var snapshot = Capture(source);
                Assert.That(snapshot.ActiveTouchCount, Is.EqualTo(2));
                Assert.That(snapshot.Touches, Has.Length.EqualTo(2));
                Assert.That(snapshot.Touches[0].TouchId, Is.EqualTo(42));
                Assert.That(snapshot.Touches[1].TouchId, Is.EqualTo(17));
                yield return null;
            }
            finally
            {
                if (sourceObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(sourceObject);
                }

                if (device != null && device.added)
                {
                    InputSystem.RemoveDevice(device);
                }
            }
        }

        [UnityTest]
        public IEnumerator Source_TwoTouchesPreserveMixedStationaryAndMovedPhases()
        {
            Touchscreen device = null;
            GameObject sourceObject = null;
            try
            {
                device = InputSystem.AddDevice<Touchscreen>();
                sourceObject = new GameObject("Phase6DecorationMixedPhaseSource");
                var source = sourceObject.AddComponent<InputSystemDecorationTouchSource>();
                BeginTouch(42, new Vector2(10f, 10f), queueEventOnly: true, screen: device);
                BeginTouch(17, new Vector2(20f, 20f), queueEventOnly: true, screen: device);
                InputSystem.Update();
                Capture(source);
                yield return null;

                SetTouch(
                    42,
                    InputTouchPhase.Stationary,
                    new Vector2(10f, 10f),
                    queueEventOnly: true,
                    screen: device);
                MoveTouch(
                    17,
                    new Vector2(23f, 25f),
                    new Vector2(3f, 5f),
                    queueEventOnly: true,
                    screen: device);
                InputSystem.Update();
                var snapshot = Capture(source);

                Assert.That(snapshot.ActiveTouchCount, Is.EqualTo(2));
                Assert.That(snapshot.Touches, Has.Length.EqualTo(2));
                Assert.That(snapshot.Touches[0].TouchId, Is.EqualTo(42));
                Assert.That(snapshot.Touches[0].Phase, Is.EqualTo(InputTouchPhase.Stationary));
                Assert.That(snapshot.Touches[1].TouchId, Is.EqualTo(17));
                Assert.That(snapshot.Touches[1].Phase, Is.EqualTo(InputTouchPhase.Moved));
                Assert.That(snapshot.Touches[1].Delta,
                    Is.EqualTo(new Vector2(3f, 5f)).Using(Vector2ComparerWithEqualsOperator.Instance));
            }
            finally
            {
                if (sourceObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(sourceObject);
                }

                if (device != null && device.added)
                {
                    InputSystem.RemoveDevice(device);
                }
            }
        }

        [UnityTest]
        public IEnumerator Source_EnableDisableBalancesOnlyItsOwnershipAndClearsCache()
        {
            Touchscreen device = null;
            GameObject sourceObject = null;
            InputSystemDecorationTouchSource source = null;
            var ownsExternalSupport = false;
            try
            {
                device = InputSystem.AddDevice<Touchscreen>();
                sourceObject = new GameObject("Phase6DecorationLifecycleSource");
                EnhancedTouchSupport.Enable();
                ownsExternalSupport = true;
                source = sourceObject.AddComponent<InputSystemDecorationTouchSource>();
                Assert.That(EnhancedTouchSupport.enabled, Is.True);
                BeginTouch(5, new Vector2(5f, 5f), screen: device);
                AssertSingle(source, 5, new Vector2(5f, 5f), Vector2.zero, InputTouchPhase.Began);

                source.enabled = false;
                Assert.That(EnhancedTouchSupport.enabled, Is.True,
                    "Disabling the source must release only the source-owned Enable call.");
                EndTouch(5, new Vector2(5f, 5f), screen: device);
                yield return null;

                source.enabled = true;
                var refreshed = Capture(source);
                Assert.That(refreshed.FrameNumber, Is.EqualTo(Time.frameCount));
                Assert.That(refreshed.Touches, Is.Empty,
                    "Re-enable must not return the pre-disable cached Began snapshot.");

                source.enabled = false;
                Assert.That(EnhancedTouchSupport.enabled, Is.True,
                    "The separately owned EnhancedTouch lifetime must remain after source disable.");
                EnhancedTouchSupport.Disable();
                ownsExternalSupport = false;
                Assert.That(EnhancedTouchSupport.enabled, Is.False,
                    "Balanced source and external lifetimes must be observable before cleanup destroys anything.");
            }
            finally
            {
                if (source != null)
                {
                    source.enabled = false;
                }

                if (ownsExternalSupport)
                {
                    EnhancedTouchSupport.Disable();
                }

                if (sourceObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(sourceObject);
                }

                if (device != null && device.added)
                {
                    InputSystem.RemoveDevice(device);
                }
            }
        }

        [UnityTest]
        public IEnumerator Task9Support_SourceDisableWithTwoActiveTouchesClearsCacheAndReleasesOnlyItsEnhancedTouchOwnership()
        {
            Touchscreen device = null;
            GameObject sourceObject = null;
            InputSystemDecorationTouchSource source = null;
            var externalEnhancedTouchOwner = false;
            try
            {
                EnhancedTouchSupport.Enable();
                externalEnhancedTouchOwner = true;
                device = InputSystem.AddDevice<Touchscreen>();
                sourceObject = new GameObject("Task9Support_TwoTouchSource");
                source = sourceObject.AddComponent<InputSystemDecorationTouchSource>();

                BeginTouch(901, new Vector2(20f, 30f), queueEventOnly: true, screen: device);
                BeginTouch(902, new Vector2(60f, 30f), queueEventOnly: true, screen: device);
                InputSystem.Update();
                var active = Capture(source);
                Assert.That(active.ActiveTouchCount, Is.EqualTo(2));
                Assert.That(active.Touches.Select(item => item.TouchId),
                    Is.EqualTo(new[] { 901, 902 }));

                source.enabled = false;
                Assert.That(EnhancedTouchSupport.enabled, Is.True,
                    "Source disable must release only the source-owned EnhancedTouch lease.");
                EndTouch(901, new Vector2(20f, 30f), queueEventOnly: true, screen: device);
                CancelTouch(902, new Vector2(60f, 30f), queueEventOnly: true, screen: device);
                InputSystem.Update();
                yield return null;

                source.enabled = true;
                Assert.That(Capture(source).Touches, Is.Empty,
                    "Re-enable must not replay the two-contact cache owned before disable.");
                yield return null;
                BeginTouch(903, new Vector2(90f, 40f), screen: device);
                AssertSingle(source, 903, new Vector2(90f, 40f), Vector2.zero,
                    InputTouchPhase.Began);
                EndTouch(903, new Vector2(90f, 40f), screen: device);
                yield return null;
            }
            finally
            {
                if (source != null)
                {
                    source.enabled = false;
                }

                if (externalEnhancedTouchOwner)
                {
                    EnhancedTouchSupport.Disable();
                }

                if (sourceObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(sourceObject);
                }

                if (device != null && device.added)
                {
                    InputSystem.RemoveDevice(device);
                }
            }
        }

        [UnityTest]
        public IEnumerator Task9Support_SourceFixtureOwnedTouchRemovalPreservesUnrelatedMouseKeyboardAndCurrentDeviceState()
        {
            Mouse mouse = null;
            Keyboard keyboard = null;
            Touchscreen touch = null;
            GameObject sourceObject = null;
            try
            {
                mouse = InputSystem.AddDevice<Mouse>();
                keyboard = InputSystem.AddDevice<Keyboard>();
                mouse.MakeCurrent();
                keyboard.MakeCurrent();
                var expectedMouse = Mouse.current;
                var expectedKeyboard = Keyboard.current;
                touch = InputSystem.AddDevice<Touchscreen>();
                sourceObject = new GameObject("Task9Support_DeviceIsolationSource");
                var source = sourceObject.AddComponent<InputSystemDecorationTouchSource>();

                BeginTouch(911, new Vector2(16f, 24f), screen: touch);
                Assert.That(Capture(source).ActiveTouchCount, Is.EqualTo(1));
                source.enabled = false;
                EndTouch(911, new Vector2(16f, 24f), screen: touch);
                yield return null;
                InputSystem.RemoveDevice(touch);
                touch = null;

                Assert.That(mouse.added, Is.True);
                Assert.That(keyboard.added, Is.True);
                Assert.That(Mouse.current, Is.SameAs(expectedMouse));
                Assert.That(Keyboard.current, Is.SameAs(expectedKeyboard));
                Assert.That(InputSystem.devices.Contains(mouse), Is.True);
                Assert.That(InputSystem.devices.Contains(keyboard), Is.True);
            }
            finally
            {
                if (sourceObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(sourceObject);
                }

                if (touch != null && touch.added)
                {
                    InputSystem.RemoveDevice(touch);
                }

                if (keyboard != null && keyboard.added)
                {
                    InputSystem.RemoveDevice(keyboard);
                }

                if (mouse != null && mouse.added)
                {
                    InputSystem.RemoveDevice(mouse);
                }
            }
        }

        private static void AssertSingle(
            InputSystemDecorationTouchSource source,
            int expectedId,
            Vector2 expectedPosition,
            Vector2 expectedDelta,
            InputTouchPhase expectedPhase)
        {
            var snapshot = Capture(source);
            Assert.That(snapshot.Touches, Has.Length.EqualTo(1));
            Assert.That(snapshot.ActiveTouchCount, Is.EqualTo(
                expectedPhase == InputTouchPhase.Began
                || expectedPhase == InputTouchPhase.Moved
                || expectedPhase == InputTouchPhase.Stationary
                    ? 1
                    : 0));
            Assert.That(snapshot.Touches[0].TouchId, Is.EqualTo(expectedId));
            Assert.That(snapshot.Touches[0].Position,
                Is.EqualTo(expectedPosition).Using(Vector2ComparerWithEqualsOperator.Instance));
            Assert.That(snapshot.Touches[0].Delta,
                Is.EqualTo(expectedDelta).Using(Vector2ComparerWithEqualsOperator.Instance));
            Assert.That(snapshot.Touches[0].Phase, Is.EqualTo(expectedPhase));
        }

        private static void AssertSnapshotsEqual(
            TouchSnapshot expected,
            TouchSnapshot actual,
            string message)
        {
            Assert.That(actual.FrameNumber, Is.EqualTo(expected.FrameNumber), message);
            Assert.That(actual.ActiveTouchCount, Is.EqualTo(expected.ActiveTouchCount), message);
            Assert.That(actual.Touches, Is.EqualTo(expected.Touches), message);
        }

        private static TouchSnapshot Capture(InputSystemDecorationTouchSource source)
        {
            // ref struct stays entirely inside this synchronous helper.
            var frame = source.ReadFrame();
            var points = new DecorationTouchPoint[frame.Touches.Length];
            frame.Touches.CopyTo(points);
            return new TouchSnapshot(frame.FrameNumber, frame.ActiveTouchCount, points);
        }

        private static TouchSnapshot Capture(IDecorationTouchSource source)
        {
            var frame = source.ReadFrame();
            var points = new DecorationTouchPoint[frame.Touches.Length];
            frame.Touches.CopyTo(points);
            return new TouchSnapshot(frame.FrameNumber, frame.ActiveTouchCount, points);
        }

        private readonly struct TouchSnapshot
        {
            public TouchSnapshot(int frameNumber, int activeTouchCount, DecorationTouchPoint[] touches)
            {
                FrameNumber = frameNumber;
                ActiveTouchCount = activeTouchCount;
                Touches = touches;
            }

            public int FrameNumber { get; }
            public int ActiveTouchCount { get; }
            public DecorationTouchPoint[] Touches { get; }
        }
    }
}

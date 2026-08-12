using System.Collections;
using System.Collections.Generic;
using AnimalCafe.UI.Components;
using AnimalCafe.UI.Foundation;
using AnimalCafe.Core.Time;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using UnityEngine.UI;
using InputTouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace AnimalCafe.Tests.PlayMode
{
    public sealed class Phase5ContainerNavigationPlayModeTests
    {
        [UnityTest]
        public IEnumerator IT010_ConfirmTouch_InvokesOnceAndClosesOnlyTopModal()
        {
            using (var fixture = new ContainerTouchFixture())
            {
                var navigation = new UiNavigationCoordinator();
                var lowerState = CriticalModalState("Lower");
                var topState = CriticalModalState("Top");
                var lower = fixture.CreateModal("Lower", new Vector2(-250f, 0f));
                var top = fixture.CreateModal("Top", new Vector2(250f, 0f));
                var confirmCount = 0;
                lower.View.Configure(navigation, lowerState, lower.Confirm, lower.Cancel, lower.Outside, false);
                top.View.Configure(navigation, topState, top.Confirm, top.Cancel, top.Outside, false);
                top.View.Confirmed += () => confirmCount++;
                lower.View.Open();
                top.View.Open();

                fixture.QueueTap(top.ConfirmPosition);
                yield return null;
                yield return null;

                Assert.That(confirmCount, Is.EqualTo(1));
                Assert.That(topState.IsOpen, Is.False);
                Assert.That(lowerState.IsOpen, Is.True);

                fixture.QueueTap(top.ConfirmPosition);
                yield return null;
                yield return null;

                Assert.That(confirmCount, Is.EqualTo(1));
                Assert.That(lowerState.IsOpen, Is.True);
            }
        }

        [UnityTest]
        public IEnumerator IT013_OrdinaryBottomSheet_ClosesThroughOutsideTouchAndSharedBack()
        {
            using (var fixture = new ContainerTouchFixture())
            {
                var navigation = new UiNavigationCoordinator();
                var state = new UiView(
                    "OrdinarySheet", UiViewKind.BottomSheet, UiPausePolicy.ContinueGame,
                    UiOutsideDismissPolicy.Dismissible);
                var sheet = fixture.CreateBottomSheet();
                sheet.View.Configure(navigation, state, sheet.Outside);
                sheet.View.Open();

                fixture.QueueTap(sheet.OutsidePosition);
                yield return null;
                yield return null;
                Assert.That(state.IsOpen, Is.False);

                sheet.View.Open();
                Assert.That(sheet.View.TryHandleBack(), Is.True);
                Assert.That(state.IsOpen, Is.False);
            }
        }

        [UnityTest]
        public IEnumerator IT014_CriticalModal_OutsideAndBlockedBackStayOpenButCancelTouchCloses()
        {
            using (var fixture = new ContainerTouchFixture())
            {
                var navigation = new UiNavigationCoordinator();
                var state = CriticalModalState("Critical");
                var modal = fixture.CreateModal("Critical", Vector2.zero);
                modal.View.Configure(navigation, state, modal.Confirm, modal.Cancel, modal.Outside, false);
                modal.View.Open();

                fixture.QueueTap(modal.OutsidePosition);
                yield return null;
                yield return null;
                Assert.That(state.IsOpen, Is.True);
                Assert.That(modal.View.TryHandleBack(), Is.False);
                Assert.That(state.IsOpen, Is.True);

                fixture.QueueTap(modal.CancelPosition);
                yield return null;
                yield return null;
                Assert.That(state.IsOpen, Is.False);

                modal.View.Configure(
                    navigation, state, modal.Confirm, modal.Cancel, modal.Outside, true);
                modal.View.Open();
                Assert.That(modal.View.TryHandleBack(), Is.True);
                Assert.That(state.IsOpen, Is.False);
            }
        }

        [UnityTest]
        public IEnumerator IT027_DisableDuringTransition_ReleasesEveryOwnedInteractionResource()
        {
            using (var fixture = new ContainerTouchFixture())
            {
                var navigation = new UiNavigationCoordinator();
                var pointerBoundary = new UiPointerBoundary();
                var gameTime = new FakeGameTimeService(GameSpeed.Normal);
                var pause = new UiPauseCoordinator(gameTime);
                var frost = new StrongFrostLease(isStrongFrostSupported: true);
                var state = CriticalModalState("Interruptible");
                var modal = fixture.CreateModal("Interruptible", Vector2.zero);
                modal.Panel.Configure(fixture.Theme, UiPanelStyle.StrongFrost, frost);
                modal.View.Configure(
                    navigation, state, modal.Confirm, modal.Cancel, modal.Outside, false);
                modal.View.ConfigureLifecycle(
                    pause, pointerBoundary, modal.Group, new UiTransitionRunner(() => false), 1f);
                modal.View.Open();
                yield return null;

                Assert.That(gameTime.CurrentSpeed, Is.EqualTo(GameSpeed.Paused));
                Assert.That(modal.Group.blocksRaycasts, Is.True);

                fixture.QueueTouchBegan(modal.ContentPosition);
                yield return null;
                Assert.That(
                    pointerBoundary.GetOwnership(fixture.PointerId),
                    Is.EqualTo(UiPointerOwnership.Ui));

                modal.Root.SetActive(false);
                yield return null;

                Assert.That(modal.Group.blocksRaycasts, Is.False);
                Assert.That(gameTime.CurrentSpeed, Is.EqualTo(GameSpeed.Normal));
                Assert.That(pointerBoundary.GetOwnership(fixture.PointerId), Is.EqualTo(UiPointerOwnership.None));
                Assert.That(pointerBoundary.CanProcessScenePointer(fixture.PointerId), Is.True);
                Assert.That(state.IsOpen, Is.False);
                var nextOwner = frost.Acquire(new object());
                Assert.That(nextOwner.ResolvedStyle, Is.EqualTo(UiPanelStyle.StrongFrost));
                nextOwner.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator Fix1_FullyConfiguredModal_EveryClosePathReleasesAllOwnedResources()
        {
            using (var fixture = new ContainerTouchFixture())
            {
                foreach (var closePath in new[]
                         {
                             ModalClosePath.Back,
                             ModalClosePath.Outside,
                             ModalClosePath.Confirm,
                             ModalClosePath.Cancel
                         })
                {
                    var navigation = new UiNavigationCoordinator();
                    var pointerBoundary = new UiPointerBoundary();
                    var gameTime = new FakeGameTimeService(GameSpeed.Normal);
                    var pause = new UiPauseCoordinator(gameTime);
                    var frost = new StrongFrostLease(isStrongFrostSupported: true);
                    var state = new UiView(
                        "Lifecycle-" + closePath,
                        UiViewKind.Modal,
                        UiPausePolicy.PauseGame,
                        UiOutsideDismissPolicy.Dismissible);
                    var modal = fixture.CreateModal("Lifecycle-" + closePath, Vector2.zero);
                    modal.Panel.Configure(fixture.Theme, UiPanelStyle.StrongFrost, frost);
                    modal.View.Configure(
                        navigation, state, modal.Confirm, modal.Cancel, modal.Outside, true);
                    modal.View.ConfigureLifecycle(
                        pause, pointerBoundary, modal.Group, new UiTransitionRunner(() => false), 1f);
                    modal.View.Open();
                    yield return null;

                    fixture.QueueTap(modal.ContentPosition);
                    yield return null;
                    yield return null;
                    var ownedPointerId = fixture.PointerId;

                    Assert.That(gameTime.CurrentSpeed, Is.EqualTo(GameSpeed.Paused));
                    Assert.That(modal.Group.blocksRaycasts, Is.True);
                    Assert.That(pointerBoundary.GetOwnership(ownedPointerId), Is.EqualTo(UiPointerOwnership.Ui));
                    Assert.That(pointerBoundary.CanProcessScenePointer(9876), Is.False);
                    Assert.That(state.IsOpen, Is.True);
                    Assert.That(modal.Group.alpha, Is.GreaterThan(0f).And.LessThan(1f));
                    var competingOwner = frost.Acquire(new object());
                    Assert.That(competingOwner.ResolvedStyle, Is.EqualTo(UiPanelStyle.LightFrost));
                    competingOwner.Dispose();

                    switch (closePath)
                    {
                        case ModalClosePath.Back:
                            Assert.That(modal.View.TryHandleBack(), Is.True);
                            break;
                        case ModalClosePath.Outside:
                            fixture.QueueTap(modal.OutsidePosition);
                            yield return null;
                            yield return null;
                            break;
                        case ModalClosePath.Confirm:
                            fixture.QueueTap(modal.ConfirmPosition);
                            yield return null;
                            yield return null;
                            break;
                        case ModalClosePath.Cancel:
                            fixture.QueueTap(modal.CancelPosition);
                            yield return null;
                            yield return null;
                            break;
                    }

                    Assert.That(state.IsOpen, Is.False, closePath + " must close navigation state.");
                    Assert.That(gameTime.CurrentSpeed, Is.EqualTo(GameSpeed.Normal), closePath.ToString());
                    Assert.That(modal.Group.blocksRaycasts, Is.False, closePath.ToString());
                    Assert.That(modal.Group.interactable, Is.False, closePath.ToString());
                    Assert.That(modal.Group.alpha, Is.Zero, closePath.ToString());
                    Assert.That(pointerBoundary.GetOwnership(ownedPointerId), Is.EqualTo(UiPointerOwnership.None));
                    Assert.That(pointerBoundary.CanProcessScenePointer(9876), Is.True);
                    var nextOwner = frost.Acquire(new object());
                    Assert.That(nextOwner.ResolvedStyle, Is.EqualTo(UiPanelStyle.StrongFrost));
                    nextOwner.Dispose();
                    Object.DestroyImmediate(modal.Root);
                }
            }
        }

        [UnityTest]
        public IEnumerator Fix1_BottomSheet_TransitionsAndEveryLifecycleExitCleanAndReopenSafely()
        {
            var originalTimeScale = Time.timeScale;
            using (var fixture = new ContainerTouchFixture())
            {
                try
                {
                    Time.timeScale = 0f;
                    var navigation = new UiNavigationCoordinator();
                    var pointerBoundary = new UiPointerBoundary();
                    var gameTime = new FakeGameTimeService(GameSpeed.Normal);
                    var pause = new UiPauseCoordinator(gameTime);
                    var state = OrdinarySheetState("LifecycleSheet");
                    var sheet = fixture.CreateBottomSheet();
                    sheet.View.Configure(navigation, state, sheet.Outside);
                    sheet.View.ConfigureLifecycle(
                        pause, pointerBoundary, sheet.Group,
                        new UiTransitionRunner(() => false), 0.03f);

                    sheet.View.Open();
                    Assert.That(sheet.Group.blocksRaycasts, Is.True);
                    yield return new WaitForSecondsRealtime(0.06f);
                    Assert.That(sheet.Group.alpha, Is.EqualTo(1f));
                    Assert.That(gameTime.CurrentSpeed, Is.EqualTo(GameSpeed.Normal));

                    fixture.QueueTap(sheet.ContentPosition);
                    yield return null;
                    yield return null;
                    var outsidePointerId = fixture.PointerId;
                    Assert.That(
                        pointerBoundary.GetOwnership(outsidePointerId),
                        Is.EqualTo(UiPointerOwnership.Ui));

                    fixture.QueueTap(sheet.OutsidePosition);
                    yield return new WaitForSecondsRealtime(0.06f);
                    AssertSheetClosed(sheet, state, navigation, pointerBoundary, outsidePointerId);

                    sheet.View.Open();
                    yield return new WaitForSecondsRealtime(0.06f);
                    Assert.That(state.IsOpen, Is.True);
                    Assert.That(sheet.Group.alpha, Is.EqualTo(1f));
                    Assert.That(sheet.View.TryHandleBack(), Is.True);
                    yield return new WaitForSecondsRealtime(0.06f);
                    AssertSheetClosed(sheet, state, navigation, pointerBoundary, outsidePointerId);

                    sheet.View.Open();
                    yield return null;
                    fixture.QueueTap(sheet.ContentPosition);
                    yield return null;
                    yield return null;
                    var disablePointerId = fixture.PointerId;
                    sheet.Root.SetActive(false);
                    sheet.Root.SetActive(false);
                    AssertSheetClosed(sheet, state, navigation, pointerBoundary, disablePointerId);

                    sheet.Root.SetActive(true);
                    sheet.View.Open();
                    yield return new WaitForSecondsRealtime(0.06f);
                    Assert.That(state.IsOpen, Is.True);
                    Assert.That(sheet.Group.alpha, Is.EqualTo(1f));

                    var replacementState = OrdinarySheetState("ReplacementSheet");
                    var replacementOutside = fixture.CreateActionButton(
                        "ReplacementOutside", new Vector2(200f, 120f));
                    sheet.View.Configure(navigation, replacementState, replacementOutside.Button);
                    sheet.View.ConfigureLifecycle(
                        pause, pointerBoundary, sheet.Group,
                        new UiTransitionRunner(() => false), 0.03f);
                    sheet.View.Open();
                    yield return new WaitForSecondsRealtime(0.06f);

                    fixture.QueueTap(sheet.OutsidePosition);
                    yield return null;
                    yield return null;
                    Assert.That(replacementState.IsOpen, Is.True, "Old outside listener must be removed.");

                    fixture.QueueTap(replacementOutside.Position);
                    yield return new WaitForSecondsRealtime(0.06f);
                    Assert.That(replacementState.IsOpen, Is.False);
                    Assert.That(sheet.Group.blocksRaycasts, Is.False);

                    var destroyState = OrdinarySheetState("DestroySheet");
                    var destroySheet = fixture.CreateBottomSheet();
                    destroySheet.View.Configure(navigation, destroyState, destroySheet.Outside);
                    destroySheet.View.ConfigureLifecycle(
                        pause, pointerBoundary, destroySheet.Group,
                        new UiTransitionRunner(() => false), 1f);
                    destroySheet.View.Open();
                    yield return null;
                    fixture.QueueTap(destroySheet.ContentPosition);
                    yield return null;
                    yield return null;
                    var destroyPointerId = fixture.PointerId;
                    Object.DestroyImmediate(destroySheet.Root);

                    Assert.That(destroyState.IsOpen, Is.False);
                    Assert.That(navigation.ActiveBottomSheet, Is.Null);
                    Assert.That(
                        pointerBoundary.GetOwnership(destroyPointerId),
                        Is.EqualTo(UiPointerOwnership.None));
                    Assert.That(gameTime.CurrentSpeed, Is.EqualTo(GameSpeed.Normal));
                }
                finally
                {
                    Time.timeScale = originalTimeScale;
                }
            }
        }

        [UnityTest]
        public IEnumerator Fix1_NonTopModalActions_CannotCloseOrConfirmEitherModal()
        {
            using (var fixture = new ContainerTouchFixture())
            {
                var navigation = new UiNavigationCoordinator();
                var lowerState = DismissibleModalState("GuardedLower");
                var topState = DismissibleModalState("GuardedTop");
                var lower = fixture.CreateModal("GuardedLower", new Vector2(-200f, 0f));
                var top = fixture.CreateModal("GuardedTop", new Vector2(200f, 0f));
                var lowerConfirmCount = 0;
                lower.View.Configure(
                    navigation, lowerState, lower.Confirm, lower.Cancel, lower.Outside, true);
                top.View.Configure(
                    navigation, topState, top.Confirm, top.Cancel, top.Outside, true);
                lower.View.Confirmed += () => lowerConfirmCount++;
                lower.View.Open();
                top.View.Open();

                // Put the lower actions above the fixture blocker to prove the component's own
                // top-of-stack guard, while retaining the real EventSystem/Button route.
                lower.Confirm.transform.SetAsLastSibling();
                fixture.QueueTap(lower.ConfirmPosition);
                yield return null;
                yield return null;
                Assert.That(lowerConfirmCount, Is.Zero);
                Assert.That(lowerState.IsOpen, Is.True);
                Assert.That(topState.IsOpen, Is.True);

                lower.Outside.transform.SetAsLastSibling();
                fixture.QueueTap(lower.OutsidePosition);
                yield return null;
                yield return null;
                Assert.That(lowerState.IsOpen, Is.True);
                Assert.That(topState.IsOpen, Is.True);

                Assert.That(lower.View.TryHandleBack(), Is.False);
                Assert.That(lowerState.IsOpen, Is.True);
                Assert.That(topState.IsOpen, Is.True);
            }
        }

        [UnityTest]
        public IEnumerator Fix1_Modal_DestroyReopenDisableAndReconfigure_AreIdempotentAndReleaseOldState()
        {
            using (var fixture = new ContainerTouchFixture())
            {
                var navigation = new UiNavigationCoordinator();
                var pointerBoundary = new UiPointerBoundary();
                var gameTime = new FakeGameTimeService(GameSpeed.Normal);
                var pause = new UiPauseCoordinator(gameTime);
                var frost = new StrongFrostLease(isStrongFrostSupported: true);
                var state = CriticalModalState("ReopenModal");
                var modal = fixture.CreateModal("ReopenModal", Vector2.zero);
                modal.Panel.Configure(fixture.Theme, UiPanelStyle.StrongFrost, frost);
                modal.View.Configure(
                    navigation, state, modal.Confirm, modal.Cancel, modal.Outside, false);
                modal.View.ConfigureLifecycle(
                    pause, pointerBoundary, modal.Group,
                    new UiTransitionRunner(() => false), 0.03f);
                modal.View.Open();
                yield return new WaitForSecondsRealtime(0.06f);

                fixture.QueueTap(modal.CancelPosition);
                yield return new WaitForSecondsRealtime(0.06f);
                Assert.That(state.IsOpen, Is.False);
                Assert.That(gameTime.CurrentSpeed, Is.EqualTo(GameSpeed.Normal));

                modal.View.Open();
                yield return new WaitForSecondsRealtime(0.06f);
                Assert.That(state.IsOpen, Is.True);
                Assert.That(gameTime.CurrentSpeed, Is.EqualTo(GameSpeed.Paused));
                Assert.That(modal.Panel.ResolvedStyle, Is.EqualTo(UiPanelStyle.StrongFrost));
                var reopenCompetitor = frost.Acquire(new object());
                Assert.That(reopenCompetitor.ResolvedStyle, Is.EqualTo(UiPanelStyle.LightFrost));
                reopenCompetitor.Dispose();

                modal.Root.SetActive(false);
                modal.Root.SetActive(false);
                Assert.That(state.IsOpen, Is.False);
                Assert.That(gameTime.CurrentSpeed, Is.EqualTo(GameSpeed.Normal));
                Assert.That(modal.Group.blocksRaycasts, Is.False);

                modal.Root.SetActive(true);
                modal.View.Open();
                yield return null;
                fixture.QueueTap(modal.ContentPosition);
                yield return null;
                yield return null;
                var oldPointerId = fixture.PointerId;

                var replacementNavigation = new UiNavigationCoordinator();
                var replacementState = CriticalModalState("ReplacementModal");
                var replacementConfirm = fixture.CreateActionButton(
                    "ReplacementConfirm", new Vector2(200f, 0f));
                var replacementCancel = fixture.CreateActionButton(
                    "ReplacementCancel", new Vector2(200f, -120f));
                var replacementOutside = fixture.CreateActionButton(
                    "ReplacementOutside", new Vector2(200f, 120f));
                var replacementConfirmCount = 0;
                modal.View.Configure(
                    replacementNavigation,
                    replacementState,
                    replacementConfirm.Button,
                    replacementCancel.Button,
                    replacementOutside.Button,
                    false);
                modal.View.Confirmed += () => replacementConfirmCount++;

                Assert.That(state.IsOpen, Is.False, "Reconfigure must close the old navigation state.");
                Assert.That(navigation.TryHandleBack(), Is.False);
                Assert.That(gameTime.CurrentSpeed, Is.EqualTo(GameSpeed.Normal));
                Assert.That(pointerBoundary.GetOwnership(oldPointerId), Is.EqualTo(UiPointerOwnership.None));
                Assert.That(modal.Group.blocksRaycasts, Is.False);

                modal.View.ConfigureLifecycle(
                    pause, pointerBoundary, modal.Group,
                    new UiTransitionRunner(() => false), 0.03f);
                modal.View.Open();
                yield return new WaitForSecondsRealtime(0.06f);

                fixture.QueueTap(modal.ConfirmPosition);
                yield return null;
                yield return null;
                Assert.That(replacementState.IsOpen, Is.True, "Old Confirm listener must be removed.");
                Assert.That(replacementConfirmCount, Is.Zero);

                fixture.QueueTap(replacementConfirm.Position);
                yield return new WaitForSecondsRealtime(0.06f);
                Assert.That(replacementState.IsOpen, Is.False);
                Assert.That(replacementConfirmCount, Is.EqualTo(1));

                var destroyNavigation = new UiNavigationCoordinator();
                var destroyState = CriticalModalState("DestroyModal");
                var destroyModal = fixture.CreateModal("DestroyModal", Vector2.zero);
                destroyModal.View.Configure(
                    destroyNavigation, destroyState,
                    destroyModal.Confirm, destroyModal.Cancel, destroyModal.Outside, false);
                destroyModal.View.ConfigureLifecycle(
                    pause, pointerBoundary, destroyModal.Group,
                    new UiTransitionRunner(() => false), 1f);
                destroyModal.View.Open();
                yield return null;
                fixture.QueueTap(destroyModal.ContentPosition);
                yield return null;
                yield return null;
                var destroyPointerId = fixture.PointerId;
                Object.DestroyImmediate(destroyModal.Root);

                Assert.That(destroyState.IsOpen, Is.False);
                Assert.That(destroyNavigation.TryHandleBack(), Is.False);
                Assert.That(gameTime.CurrentSpeed, Is.EqualTo(GameSpeed.Normal));
                Assert.That(
                    pointerBoundary.GetOwnership(destroyPointerId),
                    Is.EqualTo(UiPointerOwnership.None));
                Assert.That(pointerBoundary.CanProcessScenePointer(1234), Is.True);
            }
        }

        [UnityTest]
        public IEnumerator Fix2_SharedNavigationEntry_RoutesTopLifecycleAndHonorsContainerPolicies()
        {
            using (var fixture = new ContainerTouchFixture())
            {
                var navigation = new UiNavigationCoordinator();
                var pointerBoundary = new UiPointerBoundary();
                var gameTime = new FakeGameTimeService(GameSpeed.Normal);
                var pause = new UiPauseCoordinator(gameTime);
                var frost = new StrongFrostLease(isStrongFrostSupported: true);

                var sheetState = PauseGameSheetState("UnderlyingPauseSheet");
                var sheet = fixture.CreateBottomSheet();
                sheet.View.Configure(navigation, sheetState, sheet.Outside);
                sheet.View.ConfigureLifecycle(
                    pause, pointerBoundary, sheet.Group,
                    new UiTransitionRunner(() => false), 0.03f);
                sheet.View.Open();
                yield return new WaitForSecondsRealtime(0.06f);
                fixture.QueueTap(sheet.ContentPosition);
                yield return null;
                yield return null;
                var sheetPointerId = fixture.PointerId;

                var modalState = DismissiblePauseModalState("TopLifecycleModal");
                var modal = fixture.CreateModal("TopLifecycleModal", Vector2.zero);
                modal.Panel.Configure(fixture.Theme, UiPanelStyle.StrongFrost, frost);
                modal.View.Configure(
                    navigation, modalState,
                    modal.Confirm, modal.Cancel, modal.Outside, true);
                modal.View.ConfigureLifecycle(
                    pause, pointerBoundary, modal.Group,
                    new UiTransitionRunner(() => false), 0.03f);
                modal.View.Open();
                yield return new WaitForSecondsRealtime(0.06f);
                fixture.QueueTap(modal.ContentPosition);
                yield return null;
                yield return null;
                var modalPointerId = fixture.PointerId;

                Assert.That(gameTime.CurrentSpeed, Is.EqualTo(GameSpeed.Paused));
                Assert.That(pointerBoundary.CanProcessScenePointer(404), Is.False);
                Assert.That(navigation.TryHandleBack(), Is.True);
                yield return new WaitForSecondsRealtime(0.06f);

                Assert.That(modalState.IsOpen, Is.False);
                Assert.That(modal.Group.alpha, Is.Zero);
                Assert.That(modal.Group.blocksRaycasts, Is.False);
                Assert.That(
                    pointerBoundary.GetOwnership(modalPointerId),
                    Is.EqualTo(UiPointerOwnership.None));
                Assert.That(pointerBoundary.CanProcessScenePointer(404), Is.True);
                var releasedFrost = frost.Acquire(new object());
                Assert.That(releasedFrost.ResolvedStyle, Is.EqualTo(UiPanelStyle.StrongFrost));
                releasedFrost.Dispose();

                Assert.That(sheetState.IsOpen, Is.True);
                Assert.That(navigation.ActiveBottomSheet, Is.SameAs(sheetState));
                Assert.That(sheet.Group.alpha, Is.EqualTo(1f));
                Assert.That(sheet.Group.blocksRaycasts, Is.True);
                Assert.That(
                    pointerBoundary.GetOwnership(sheetPointerId),
                    Is.EqualTo(UiPointerOwnership.Ui));
                Assert.That(gameTime.CurrentSpeed, Is.EqualTo(GameSpeed.Paused));

                modal.View.Open();
                yield return new WaitForSecondsRealtime(0.06f);
                sheet.Outside.transform.SetAsLastSibling();
                fixture.QueueTap(sheet.OutsidePosition);
                yield return null;
                yield return null;
                Assert.That(modalState.IsOpen, Is.True, "Lower Sheet action cannot dismiss upward.");
                Assert.That(sheetState.IsOpen, Is.True);
                Assert.That(modal.Group.blocksRaycasts, Is.True);
                Assert.That(sheet.Group.blocksRaycasts, Is.True);

                modal.Cancel.transform.SetAsLastSibling();
                fixture.QueueTap(modal.CancelPosition);
                yield return new WaitForSecondsRealtime(0.06f);
                Assert.That(modalState.IsOpen, Is.False);

                var criticalState = CriticalModalState("SharedBackBlockedCritical");
                var critical = fixture.CreateModal("SharedBackBlockedCritical", Vector2.zero);
                critical.View.Configure(
                    navigation, criticalState,
                    critical.Confirm, critical.Cancel, critical.Outside, false);
                critical.View.ConfigureLifecycle(
                    pause, pointerBoundary, critical.Group,
                    new UiTransitionRunner(() => false), 0.03f);
                critical.View.Open();
                yield return new WaitForSecondsRealtime(0.06f);

                Assert.That(navigation.TryHandleBack(), Is.False);
                Assert.That(criticalState.IsOpen, Is.True);
                Assert.That(critical.Group.blocksRaycasts, Is.True);

                critical.Cancel.transform.SetAsLastSibling();
                fixture.QueueTap(critical.CancelPosition);
                yield return new WaitForSecondsRealtime(0.06f);
                Assert.That(criticalState.IsOpen, Is.False);

                Assert.That(navigation.TryHandleBack(), Is.True);
                yield return new WaitForSecondsRealtime(0.06f);
                Assert.That(sheetState.IsOpen, Is.False);
                Assert.That(sheet.Group.alpha, Is.Zero);
                Assert.That(sheet.Group.blocksRaycasts, Is.False);
                Assert.That(
                    pointerBoundary.GetOwnership(sheetPointerId),
                    Is.EqualTo(UiPointerOwnership.None));
                Assert.That(gameTime.CurrentSpeed, Is.EqualTo(GameSpeed.Normal));

                var disableState = PauseGameSheetState("DisablePauseSheet");
                var disableSheet = fixture.CreateBottomSheet();
                disableSheet.View.Configure(navigation, disableState, disableSheet.Outside);
                disableSheet.View.ConfigureLifecycle(
                    pause, pointerBoundary, disableSheet.Group,
                    new UiTransitionRunner(() => false), 1f);
                disableSheet.View.Open();
                yield return null;
                Assert.That(gameTime.CurrentSpeed, Is.EqualTo(GameSpeed.Paused));

                disableSheet.Root.SetActive(false);
                disableSheet.Root.SetActive(false);
                Assert.That(disableState.IsOpen, Is.False);
                Assert.That(gameTime.CurrentSpeed, Is.EqualTo(GameSpeed.Normal));
                Assert.That(disableSheet.Group.blocksRaycasts, Is.False);
            }
        }

        private static UiView CriticalModalState(string id)
        {
            return new UiView(
                id, UiViewKind.Modal, UiPausePolicy.PauseGame,
                UiOutsideDismissPolicy.NotDismissible);
        }

        private static UiView OrdinarySheetState(string id)
        {
            return new UiView(
                id, UiViewKind.BottomSheet, UiPausePolicy.ContinueGame,
                UiOutsideDismissPolicy.Dismissible);
        }

        private static UiView DismissibleModalState(string id)
        {
            return new UiView(
                id, UiViewKind.Modal, UiPausePolicy.ContinueGame,
                UiOutsideDismissPolicy.Dismissible);
        }

        private static UiView DismissiblePauseModalState(string id)
        {
            return new UiView(
                id, UiViewKind.Modal, UiPausePolicy.PauseGame,
                UiOutsideDismissPolicy.Dismissible);
        }

        private static UiView PauseGameSheetState(string id)
        {
            return new UiView(
                id, UiViewKind.BottomSheet, UiPausePolicy.PauseGame,
                UiOutsideDismissPolicy.Dismissible);
        }

        private static void AssertSheetClosed(
            BottomSheetFixture sheet,
            UiView state,
            UiNavigationCoordinator navigation,
            UiPointerBoundary pointerBoundary,
            int pointerId)
        {
            Assert.That(state.IsOpen, Is.False);
            Assert.That(navigation.ActiveBottomSheet, Is.Null);
            Assert.That(sheet.Group.alpha, Is.Zero);
            Assert.That(sheet.Group.blocksRaycasts, Is.False);
            Assert.That(sheet.Group.interactable, Is.False);
            Assert.That(pointerBoundary.GetOwnership(pointerId), Is.EqualTo(UiPointerOwnership.None));
        }

        private static Material CreateMaterial()
        {
            return new Material(Shader.Find("Hidden/InternalErrorShader"));
        }

        private enum ModalClosePath
        {
            Back,
            Outside,
            Confirm,
            Cancel
        }

        private sealed class ContainerTouchFixture : System.IDisposable
        {
            private readonly GameObject canvasObject;
            private readonly GameObject eventSystemObject;
            private readonly List<GameObject> disabledEventSystems = new List<GameObject>();
            private readonly List<GameObject> ownedObjects = new List<GameObject>();
            private readonly InputSystemUIInputModule inputModule;
            private readonly Touchscreen touchscreen;
            private readonly InputSettings.BackgroundBehavior originalBackgroundBehavior;
            private readonly InputSettings.EditorInputBehaviorInPlayMode originalEditorInputBehavior;
            private readonly bool originalRunInBackground;
            private int touchId;

            public AnimalCafeUiTheme Theme { get; }
            // InputSystemUIInputModule composes Touch pointerId from deviceId + touchId.
            // Touch 的 EventSystem pointerId 不是单纯的 deviceId。
            public int PointerId => (touchscreen.deviceId << 24) + touchId;

            public ContainerTouchFixture()
            {
                originalBackgroundBehavior = InputSystem.settings.backgroundBehavior;
                originalEditorInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
                originalRunInBackground = Application.runInBackground;
                Application.runInBackground = true;
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                InputSystem.settings.editorInputBehaviorInPlayMode =
                    InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;

                foreach (var existing in Resources.FindObjectsOfTypeAll<EventSystem>())
                {
                    if (existing.gameObject.scene.IsValid() && existing.gameObject.scene.isLoaded
                        && existing.gameObject.activeSelf)
                    {
                        disabledEventSystems.Add(existing.gameObject);
                        existing.gameObject.SetActive(false);
                    }
                }

                canvasObject = new GameObject(
                    "ContainerCanvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
                canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                Theme = ScriptableObject.CreateInstance<AnimalCafeUiTheme>();
                Theme.Materials = new UiMaterialTokens(
                    CreateMaterial(), CreateMaterial(), CreateMaterial(), CreateMaterial());
                eventSystemObject = new GameObject("ContainerEventSystem");
                eventSystemObject.AddComponent<EventSystem>();
                inputModule = eventSystemObject.AddComponent<InputSystemUIInputModule>();
                inputModule.UnassignActions();
                inputModule.AssignDefaultActions();
                touchscreen = InputSystem.AddDevice<Touchscreen>();
                Canvas.ForceUpdateCanvases();
            }

            public ModalFixture CreateModal(string name, Vector2 offset)
            {
                var root = CreateRoot(name + "Modal");
                var outside = CreateButton(name + "Outside", offset + new Vector2(0f, 120f));
                var confirm = CreateButton(name + "Confirm", offset + new Vector2(0f, 0f));
                var cancel = CreateButton(name + "Cancel", offset + new Vector2(0f, -120f));
                return new ModalFixture(
                    root.AddComponent<AnimalCafeModalView>(), outside.Button, confirm.Button, cancel.Button,
                    outside.Position, confirm.Position, cancel.Position, root,
                    root.GetComponent<CanvasGroup>(), root.GetComponent<AnimalCafePanelView>(),
                    new Vector2(Screen.width * 0.5f + 200f, Screen.height * 0.5f));
            }

            public BottomSheetFixture CreateBottomSheet()
            {
                var root = CreateRoot("BottomSheet");
                var outside = CreateButton("SheetOutside", Vector2.zero);
                return new BottomSheetFixture(
                    root.AddComponent<AnimalCafeBottomSheetView>(), outside.Button, outside.Position,
                    root, root.GetComponent<CanvasGroup>(),
                    new Vector2(Screen.width * 0.5f + 200f, Screen.height * 0.5f));
            }

            public ButtonFixture CreateActionButton(string name, Vector2 offset)
            {
                return CreateButton(name, offset);
            }

            public void QueueTap(Vector2 position)
            {
                touchId++;
                QueueTouch(position, InputTouchPhase.Began);
                InputSystem.Update();
                QueueTouch(position, InputTouchPhase.Ended);
            }

            public void QueueTouchBegan(Vector2 position)
            {
                touchId++;
                QueueTouch(position, InputTouchPhase.Began);
            }

            private GameObject CreateRoot(string name)
            {
                var root = new GameObject(
                    name, typeof(RectTransform), typeof(CanvasRenderer), typeof(CanvasGroup));
                root.transform.SetParent(canvasObject.transform, false);
                var rect = root.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                root.AddComponent<Image>();
                root.AddComponent<AnimalCafePanelView>();
                ownedObjects.Add(root);
                return root;
            }

            private ButtonFixture CreateButton(string name, Vector2 centeredOffset)
            {
                var buttonObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
                buttonObject.transform.SetParent(canvasObject.transform, false);
                var rect = buttonObject.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = centeredOffset;
                rect.sizeDelta = new Vector2(100f, 100f);
                buttonObject.AddComponent<Image>();
                var button = buttonObject.AddComponent<Button>();
                return new ButtonFixture(
                    button, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f) + centeredOffset);
            }

            private void QueueTouch(Vector2 position, InputTouchPhase phase)
            {
                InputSystem.QueueStateEvent(
                    touchscreen,
                    new TouchState
                    {
                        touchId = touchId,
                        phase = phase,
                        position = position,
                        pressure = phase == InputTouchPhase.Ended ? 0f : 1f
                    });
            }

            public void Dispose()
            {
                inputModule.UnassignActions();
                if (touchscreen.added)
                {
                    InputSystem.RemoveDevice(touchscreen);
                }

                foreach (var owned in ownedObjects)
                {
                    Object.DestroyImmediate(owned);
                }

                Object.DestroyImmediate(eventSystemObject);
                Object.DestroyImmediate(canvasObject);
                Object.DestroyImmediate(Theme.Materials.Solid);
                Object.DestroyImmediate(Theme.Materials.LightFrost);
                Object.DestroyImmediate(Theme.Materials.StrongFrost);
                Object.DestroyImmediate(Theme.Materials.StrongFrostFallback);
                Object.DestroyImmediate(Theme);
                foreach (var disabled in disabledEventSystems)
                {
                    if (disabled != null)
                    {
                        disabled.SetActive(true);
                    }
                }

                InputSystem.settings.backgroundBehavior = originalBackgroundBehavior;
                InputSystem.settings.editorInputBehaviorInPlayMode = originalEditorInputBehavior;
                Application.runInBackground = originalRunInBackground;
            }
        }

        public readonly struct ButtonFixture
        {
            public ButtonFixture(Button button, Vector2 position) { Button = button; Position = position; }
            public Button Button { get; }
            public Vector2 Position { get; }
        }

        private readonly struct ModalFixture
        {
            public ModalFixture(AnimalCafeModalView view, Button outside, Button confirm, Button cancel,
                Vector2 outsidePosition, Vector2 confirmPosition, Vector2 cancelPosition,
                GameObject root, CanvasGroup group, AnimalCafePanelView panel, Vector2 contentPosition)
            {
                View = view; Outside = outside; Confirm = confirm; Cancel = cancel;
                OutsidePosition = outsidePosition; ConfirmPosition = confirmPosition;
                CancelPosition = cancelPosition;
                Root = root; Group = group; Panel = panel; ContentPosition = contentPosition;
            }
            public AnimalCafeModalView View { get; }
            public Button Outside { get; }
            public Button Confirm { get; }
            public Button Cancel { get; }
            public Vector2 OutsidePosition { get; }
            public Vector2 ConfirmPosition { get; }
            public Vector2 CancelPosition { get; }
            public GameObject Root { get; }
            public CanvasGroup Group { get; }
            public AnimalCafePanelView Panel { get; }
            public Vector2 ContentPosition { get; }
        }

        private sealed class FakeGameTimeService : IGameTimeService
        {
            public FakeGameTimeService(GameSpeed speed) { CurrentSpeed = speed; }
            public GameSpeed CurrentSpeed { get; private set; }
            public bool TrySetSpeed(GameSpeed speed) { CurrentSpeed = speed; return true; }
        }

        private readonly struct BottomSheetFixture
        {
            public BottomSheetFixture(
                AnimalCafeBottomSheetView view, Button outside, Vector2 position,
                GameObject root, CanvasGroup group, Vector2 contentPosition)
            {
                View = view; Outside = outside; OutsidePosition = position;
                Root = root; Group = group; ContentPosition = contentPosition;
            }
            public AnimalCafeBottomSheetView View { get; }
            public Button Outside { get; }
            public Vector2 OutsidePosition { get; }
            public GameObject Root { get; }
            public CanvasGroup Group { get; }
            public Vector2 ContentPosition { get; }
        }
    }
}

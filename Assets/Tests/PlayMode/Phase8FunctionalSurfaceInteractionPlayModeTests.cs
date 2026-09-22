using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AnimalCafe.Content;
using AnimalCafe.Camera;
using AnimalCafe.Core.Time;
using AnimalCafe.Decoration;
using AnimalCafe.Decoration.Input;
using AnimalCafe.Layout;
using AnimalCafe.UI.Decoration;
using AnimalCafe.UI.Components;
using AnimalCafe.UI.Feedback;
using AnimalCafe.UI.Foundation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using InputTouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace AnimalCafe.Tests.PlayMode
{
    public sealed class Phase8FunctionalSurfaceInteractionPlayModeTests
    {
        private const string SupportDefinitionId = "furniture.counter.long";
        private const string RegisterDefinitionId = "equipment.cash-register";
        private const string CoffeeMachineDefinitionId = "equipment.coffee-machine";
        private const string SupportInstanceId = "11111111111111111111111111111111";
        private const string OtherSupportInstanceId = "22222222222222222222222222222222";
        private const string PreviewMountedIdA = "33333333333333333333333333333331";
        private const string PreviewMountedIdB = "33333333333333333333333333333332";
        private const string PreviewMountedOtherId = "33333333333333333333333333333333";
        private const string PreviewPickUpIdA = "44444444444444444444444444444441";
        private const string PreviewPickUpIdB = "44444444444444444444444444444442";

        [TestCase(DecorationCatalogueItemKind.CashRegister, RegisterDefinitionId)]
        [TestCase(DecorationCatalogueItemKind.CoffeeMachine, CoffeeMachineDefinitionId)]
        public void NewEquipment_StartsAtVisibleCenterSlot_WithoutMovingCameraOrPublishing(
            DecorationCatalogueItemKind kind, string definitionId)
        {
            using var h = new FunctionalViewHarness();
            h.Camera.transform.position = new Vector3(2.5f, 10f, .5f);
            h.Camera.orthographicSize = .65f;
            h.Camera.aspect = 1f;
            var cameraPosition = h.Camera.transform.position;
            var version = h.Runtime.ReadinessVersion;
            Assert.That(h.SelectMountedCatalogueItem(kind, definitionId), Is.True);
            Assert.That(h.Controller.ActiveFunctionalSurfacePreview.Address, Is.EqualTo(Address("slot.2")));
            Assert.That(h.Controller.ActiveFunctionalSurfacePreview.CanConfirm, Is.True);
            Assert.That(h.Camera.transform.position, Is.EqualTo(cameraPosition));
            Assert.That(h.Runtime.ReadinessVersion, Is.EqualTo(version));
            Assert.That(h.Scenario.Functional.MountedInstances, Is.Empty);
        }

        [Test]
        public void NewEquipment_SkipsOccupiedCenter_AndBreaksEqualDistanceStably()
        {
            using var h = new FunctionalViewHarness();
            Assert.That(h.Scenario.Functional.PlaceMounted(new SurfaceMountedInstance(
                PreviewMountedIdA, RegisterDefinitionId, Address("slot.2"), FurnitureRotation.Degrees0)).Succeeded, Is.True);
            h.Camera.transform.position = new Vector3(2.5f, 10f, .5f);
            h.Camera.orthographicSize = 2f;
            h.Camera.aspect = 1f;
            Assert.That(h.SelectMountedCatalogueItem(DecorationCatalogueItemKind.CoffeeMachine, CoffeeMachineDefinitionId), Is.True);
            Assert.That(h.Controller.ActiveFunctionalSurfacePreview.Address, Is.EqualTo(Address("slot.1")));
            Assert.That(h.Controller.ActiveFunctionalSurfacePreview.CanConfirm, Is.True);
            Assert.That(h.Scenario.Functional.MountedInstances.Count, Is.EqualTo(1));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void NewEquipment_NoVisibleCandidateOrCamera_UsesStableValidFallback(bool cameraAvailable)
        {
            using var h = new FunctionalViewHarness();
            h.Camera.transform.position = new Vector3(100, 10, 100);
            if (!cameraAvailable)
            {
                Set(h.Controller, "targetCamera", null);
                // Only slot ranking permits a missing Camera; rendering an action bar requires one.
                // 仅测试落点排序的 fallback，不在无 Camera 的 fixture 中渲染 ActionBar。
                var args = new object[] { DecorationCatalogueItemKind.CashRegister, RegisterDefinitionId, default(SurfaceSlotAddress) };
                var ranked = typeof(DecorationModeController).GetMethod("TryFindPreferredFunctionalSurfaceAddress",
                    BindingFlags.Instance | BindingFlags.NonPublic).Invoke(h.Controller, args);
                Assert.That(ranked, Is.True);
                Assert.That(args[2], Is.EqualTo(Address("slot.0")));
                return;
            }
            Assert.That(h.SelectMountedCatalogueItem(DecorationCatalogueItemKind.CashRegister, RegisterDefinitionId), Is.True);
            Assert.That(h.Controller.ActiveFunctionalSurfacePreview.Address, Is.EqualTo(Address("slot.0")));
        }

        [Test]
        public void Router_FunctionalSurfaceOwnsOnePointerSequence_AndCancelIsExplicit()
        {
            var address = Address("slot.0");
            var router = new DecorationTouchRouter(8f, 0f);
            var classifier = new FixedClassifier(new DecorationTouchHit(
                DecorationTouchHitKind.FunctionalSurface,
                targetId: "register.1",
                functionalSurfaceAddress: address));

            router.ProcessFrame(Frame(1, Point(7, 10, 10, InputTouchPhase.Began)), classifier);
            var dragged = router.ProcessFrame(
                Frame(2, Point(7, 30, 10, InputTouchPhase.Moved, 20, 0)), classifier);

            Assert.That(dragged.Owner, Is.EqualTo(DecorationGestureOwner.FunctionalSurface));
            Assert.That(dragged.FunctionalSurfaceDragRequested, Is.True);
            Assert.That(dragged.CameraPanRequested, Is.False);
            Assert.That(dragged.SceneDragRequested, Is.False);
            Assert.That(dragged.CurrentHit.FunctionalSurfaceAddress, Is.EqualTo(address));

            var canceled = router.ProcessFrame(
                Frame(3, Point(7, 30, 10, InputTouchPhase.Canceled)), classifier);
            Assert.That(canceled.Owner, Is.EqualTo(DecorationGestureOwner.None));
            Assert.That(canceled.GestureCanceled, Is.True);
            Assert.That(canceled.TapReleased, Is.False);
            Assert.That(router.Owner, Is.EqualTo(DecorationGestureOwner.None));
        }

        [Test]
        public void Router_UiStillWinsAndFunctionalSurfaceCannotStealItsSequence()
        {
            var router = new DecorationTouchRouter(8f, 0f);
            var classifier = new SequencedClassifier(
                new DecorationTouchHit(DecorationTouchHitKind.Ui),
                new DecorationTouchHit(
                    DecorationTouchHitKind.FunctionalSurface,
                    targetId: "pickup.1",
                    functionalSurfaceAddress: Address("slot.0")));

            router.ProcessFrame(Frame(1, Point(1, 10, 10, InputTouchPhase.Began)), classifier);
            var moved = router.ProcessFrame(
                Frame(2, Point(1, 40, 10, InputTouchPhase.Moved, 30, 0)), classifier);

            Assert.That(moved.Owner, Is.EqualTo(DecorationGestureOwner.Ui));
            Assert.That(moved.FunctionalSurfaceDragRequested, Is.False);
            Assert.That(moved.SceneDragRequested, Is.False);
            Assert.That(moved.CameraPanRequested, Is.False);
            Assert.That(classifier.CurrentCalls, Is.Zero);
        }

        [Test]
        public void Controller_MountedThenPickUp_UsesOneActiveFlowAndPublishesOnlyOnConfirm()
        {
            var root = new GameObject("Phase8FunctionalController");
            try
            {
                var layout = CreateFunctionalLayout();
                var controller = root.AddComponent<DecorationModeController>();
                Set(controller, "functionalSurfaceSession",
                    new FunctionalSurfaceDecorationSession(layout));
                var slot0 = Address("slot.0");
                var slot1 = Address("slot.1");

                Assert.That(controller.TryBeginFunctionalSurfacePreview(
                    DecorationCatalogueItemKind.CashRegister,
                    RegisterDefinitionId,
                    slot0), Is.True);
                Assert.That(controller.ActiveFunctionalSurfacePreview.Kind,
                    Is.EqualTo(FunctionalSurfacePreviewKind.MountedEquipment));
                Assert.That(layout.MountedInstances, Is.Empty,
                    "Preview is not a confirmed domain mutation.");
                Assert.That(controller.TryBeginFunctionalSurfacePreview(
                    DecorationCatalogueItemKind.PickUpPoint,
                    definitionId: null,
                    slot1), Is.False,
                    "Only one placement flow may own the controller.");
                Assert.That(controller.TryRotateFunctionalSurfacePreview(), Is.True);
                Assert.That(controller.TryConfirmFunctionalSurfacePreview(), Is.True);
                Assert.That(layout.MountedInstances, Has.Count.EqualTo(1));
                Assert.That(layout.MountedInstances[0].Rotation,
                    Is.EqualTo(FurnitureRotation.Degrees90));

                Assert.That(controller.TryBeginFunctionalSurfacePreview(
                    DecorationCatalogueItemKind.PickUpPoint,
                    definitionId: null,
                    slot1), Is.True);
                Assert.That(controller.TryRotateFunctionalSurfacePreview(), Is.False,
                    "Pick-up points intentionally have no Rotate action.");
                Assert.That(controller.TryConfirmFunctionalSurfacePreview(), Is.True);
                Assert.That(layout.PickUpPoints, Has.Count.EqualTo(1));
                Assert.That(layout.PickUpPoints[0].Address, Is.EqualTo(slot1));
                Assert.That(controller.ActiveFunctionalSurfacePreview, Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Controller_ReadinessIsStableForPreviewMoveAndCancel_ThenPublishesOnceOnConfirm()
        {
            var root = new GameObject("Phase8ReadinessPublication");
            try
            {
                var scenario = CreateScenario();
                var runtime = root.AddComponent<CafeLayoutRuntime>();
                ConfigureRuntime(runtime, scenario);
                var controller = root.AddComponent<DecorationModeController>();
                Set(controller, "layoutRuntime", runtime);
                Set(controller, "functionalSurfaceSession",
                    new FunctionalSurfaceDecorationSession(scenario.Functional));
                var originalReport = runtime.CurrentReadiness;
                var originalVersion = runtime.ReadinessVersion;

                Assert.That(controller.TryBeginFunctionalSurfacePreview(
                    DecorationCatalogueItemKind.CashRegister,
                    RegisterDefinitionId,
                    Address("slot.0")), Is.True);
                Assert.That(controller.TryMoveFunctionalSurfacePreview(Address("slot.1")), Is.True);
                Assert.That(controller.TryRotateFunctionalSurfacePreview(), Is.True);
                Assert.That(runtime.CurrentReadiness, Is.SameAs(originalReport));
                Assert.That(runtime.ReadinessVersion, Is.EqualTo(originalVersion));
                controller.CancelFunctionalSurfacePreview();
                Assert.That(runtime.CurrentReadiness, Is.SameAs(originalReport));
                Assert.That(runtime.ReadinessVersion, Is.EqualTo(originalVersion));

                Assert.That(controller.TryBeginFunctionalSurfacePreview(
                    DecorationCatalogueItemKind.CashRegister,
                    RegisterDefinitionId,
                    Address("slot.0")), Is.True);
                Assert.That(controller.TryConfirmFunctionalSurfacePreview(), Is.True);
                Assert.That(runtime.CurrentReadiness, Is.Not.SameAs(originalReport));
                Assert.That(runtime.ReadinessVersion, Is.EqualTo(originalVersion + 1));
                Assert.That(runtime.CurrentReadiness.CashRegisters.TotalCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [TestCase(ReadinessPresentationCase.MissingCoffee)]
        [TestCase(ReadinessPresentationCase.Complete)]
        [TestCase(ReadinessPresentationCase.ReadyWithInvalidExtra)]
        [TestCase(ReadinessPresentationCase.DisconnectedComplete)]
        public void Controller_ConfirmedMutation_PublishesPlayerVisibleReadinessAndStableIds(
            ReadinessPresentationCase presentationCase)
        {
            var root = new GameObject("Phase8ReadinessMessage", typeof(RectTransform));
            var labelObject = new GameObject("ReadinessLabel", typeof(RectTransform));
            try
            {
                labelObject.transform.SetParent(root.transform, false);
                var validation = root.AddComponent<ValidationMessageView>();
                validation.Configure(labelObject.AddComponent<TextMeshProUGUI>());
                var scenario = CreateReadinessScenario(presentationCase);
                var runtime = root.AddComponent<CafeLayoutRuntime>();
                ConfigureRuntime(runtime, scenario);
                var controller = root.AddComponent<DecorationModeController>();
                Set(controller, "layoutRuntime", runtime);
                Set(controller, "functionalSurfaceSession",
                    new FunctionalSurfaceDecorationSession(scenario.Functional));
                Set(controller, "isOpen", true);
                controller.ConfigurePhase8Scene(null, null, null, validation);
                var before = runtime.CurrentReadiness;
                var beforeVersion = runtime.ReadinessVersion;

                Assert.That(controller.TryBeginFunctionalSurfacePreview(
                    DecorationCatalogueItemKind.CashRegister,
                    RegisterDefinitionId,
                    ReadinessAddress(ReadinessCashSupportId)), Is.True);
                Assert.That(controller.TryConfirmFunctionalSurfacePreview(), Is.True);

                Assert.That(runtime.CurrentReadiness, Is.Not.SameAs(before));
                Assert.That(runtime.ReadinessVersion, Is.EqualTo(beforeVersion + 1));
                Assert.That(validation.IsVisible,
                    Is.EqualTo(presentationCase != ReadinessPresentationCase.Complete),
                    "Only warning/blocking readiness occupies the HUD; healthy readiness stays available as data.");
                switch (presentationCase)
                {
                    case ReadinessPresentationCase.MissingCoffee:
                        Assert.That(runtime.CurrentReadiness.CanOpenForBusiness, Is.False);
                        Assert.That(validation.CurrentMessage,
                            Does.Contain("暂时不能营业").And.Contain("还需要至少一台咖啡机"));
                        Assert.That(validation.DiagnosticIds, Is.Empty);
                        break;
                    case ReadinessPresentationCase.Complete:
                        Assert.That(runtime.CurrentReadiness.CanOpenForBusiness, Is.True);
                        Assert.That(validation.FullReadinessMessage, Does.Contain("可以营业"));
                        Assert.That(validation.FullReadinessMessage, Does.Not.Contain("但"));
                        Assert.That(validation.DiagnosticIds, Is.Empty);
                        AssertReadinessMessageSurvivesPreviewAndCancel(
                            controller,
                            runtime,
                            validation);
                        break;
                    case ReadinessPresentationCase.ReadyWithInvalidExtra:
                        Assert.That(runtime.CurrentReadiness.CanOpenForBusiness, Is.True);
                        Assert.That(validation.CurrentMessage,
                            Does.Contain("可以营业").And.Contain("互动位置被阻挡"));
                        Assert.That(validation.DiagnosticIds, Is.EqualTo(new[]
                        {
                            ReadinessExtraCashInstanceId,
                            ReadinessExtraSupportId,
                            ReadinessSlotId
                        }));
                        break;
                    case ReadinessPresentationCase.DisconnectedComplete:
                        Assert.That(runtime.CurrentReadiness.CanOpenForBusiness, Is.False);
                        Assert.That(validation.CurrentMessage,
                            Does.Contain("暂时不能营业")
                                .And.Contain("还没有完整且可到达的营业动线"));
                        Assert.That(validation.DiagnosticIds, Is.Empty);
                        break;
                    default:
                        Assert.Fail("Unhandled readiness presentation case.");
                        break;
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(labelObject);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PickUp_CreateMultiple_ExistingMoveCancelConfirmAndStore_HasNoRotate()
        {
            var root = new GameObject("Phase8MultiplePickUps");
            try
            {
                var scenario = CreateScenario();
                var controller = root.AddComponent<DecorationModeController>();
                Set(controller, "functionalSurfaceSession",
                    new FunctionalSurfaceDecorationSession(scenario.Functional));

                Assert.That(controller.TryBeginFunctionalSurfacePreview(
                    DecorationCatalogueItemKind.PickUpPoint, null, Address("slot.0")), Is.True);
                Assert.That(controller.TryRotateFunctionalSurfacePreview(), Is.False);
                Assert.That(controller.TryConfirmFunctionalSurfacePreview(), Is.True);
                Assert.That(controller.TryBeginFunctionalSurfacePreview(
                    DecorationCatalogueItemKind.PickUpPoint, null, Address("slot.1")), Is.True);
                Assert.That(controller.TryConfirmFunctionalSurfacePreview(), Is.True);
                Assert.That(scenario.Functional.PickUpPoints, Has.Count.EqualTo(2));
                Assert.That(scenario.Functional.PickUpPoints
                    .Select(point => point.InstanceId)
                    .Distinct()
                    .Count(), Is.EqualTo(2));

                var first = scenario.Functional.PickUpPoints[0];
                Assert.That(controller.TryBeginExistingFunctionalSurfacePreview(
                    FunctionalSurfacePreviewKind.PickUpPoint, first.InstanceId), Is.True);
                Assert.That(controller.TryMoveFunctionalSurfacePreview(Address("slot.2")), Is.True);
                controller.CancelFunctionalSurfacePreview();
                Assert.That(scenario.Functional.PickUpPoints.Single(
                    point => point.InstanceId == first.InstanceId).Address,
                    Is.EqualTo(Address("slot.0")));

                Assert.That(controller.TryBeginExistingFunctionalSurfacePreview(
                    FunctionalSurfacePreviewKind.PickUpPoint, first.InstanceId), Is.True);
                Assert.That(controller.TryMoveFunctionalSurfacePreview(Address("slot.2")), Is.True);
                Assert.That(controller.TryConfirmFunctionalSurfacePreview(), Is.True);
                Assert.That(scenario.Functional.PickUpPoints.Single(
                    point => point.InstanceId == first.InstanceId).Address,
                    Is.EqualTo(Address("slot.2")));
                Assert.That(controller.TryBeginExistingFunctionalSurfacePreview(
                    FunctionalSurfacePreviewKind.PickUpPoint, first.InstanceId), Is.True);
                Assert.That(controller.TryStoreFunctionalSurfacePreview(), Is.True);
                Assert.That(scenario.Functional.PickUpPoints, Has.Count.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PointerCancel_RestoresExistingPickUpConfirmedAddressAndClearsPreview()
        {
            var root = new GameObject("Phase8PointerCancel");
            try
            {
                var scenario = CreateScenario();
                var point = new PickUpPointInstance(
                    "22222222222222222222222222222222",
                    Address("slot.0"));
                Assert.That(scenario.Functional.PlacePickUp(point).Succeeded, Is.True);
                var controller = root.AddComponent<DecorationModeController>();
                Set(controller, "functionalSurfaceSession",
                    new FunctionalSurfaceDecorationSession(scenario.Functional));
                Assert.That(controller.TryBeginExistingFunctionalSurfacePreview(
                    FunctionalSurfacePreviewKind.PickUpPoint, point.InstanceId), Is.True);
                Assert.That(controller.TryMoveFunctionalSurfacePreview(Address("slot.1")), Is.True);

                var router = new DecorationTouchRouter(8f, 0f);
                var classifier = new FixedClassifier(new DecorationTouchHit(
                    DecorationTouchHitKind.FunctionalSurface,
                    targetId: point.InstanceId,
                    functionalSurfaceAddress: Address("slot.1")));
                router.ProcessFrame(Frame(1, Point(9, 10, 10, InputTouchPhase.Began)), classifier);
                var canceled = router.ProcessFrame(
                    Frame(2, Point(9, 20, 10, InputTouchPhase.Canceled)), classifier);
                controller.RouteTouchResultForActiveMode(canceled);

                Assert.That(controller.ActiveFunctionalSurfacePreview, Is.Null);
                Assert.That(scenario.Functional.PickUpPoints.Single().Address,
                    Is.EqualTo(Address("slot.0")));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [UnityTest]
        public IEnumerator Controller_RealPickUpView_PointerCancelRestoresConfirmedAndLeavesNoGhost()
        {
            using var fixture = new FunctionalViewHarness();
            const string pointId = "89898989898989898989898989898989";
            Assert.That(fixture.Scenario.Functional.PlacePickUp(new PickUpPointInstance(
                pointId,
                Address("slot.0"))).Succeeded, Is.True);
            fixture.RebuildConfirmedViews();
            Assert.That(fixture.PickUpIndicators.TryGet(pointId, out var confirmed), Is.True);
            Assert.That(confirmed.activeInHierarchy, Is.True);
            Assert.That(fixture.Controller.TryBeginExistingFunctionalSurfacePreview(
                FunctionalSurfacePreviewKind.PickUpPoint,
                pointId), Is.True);
            Assert.That(confirmed.activeSelf, Is.False);
            Assert.That(fixture.PickUpIndicators.CurrentPreview.activeInHierarchy, Is.True);

            var classifier = new FixedClassifier(new DecorationTouchHit(
                DecorationTouchHitKind.FunctionalSurface,
                targetId: pointId,
                functionalSurfaceAddress: Address("slot.1")));
            fixture.Router.ProcessFrame(
                Frame(1, Point(19, 10, 10, InputTouchPhase.Began)),
                classifier);
            var moved = fixture.Router.ProcessFrame(
                Frame(2, Point(19, 30, 10, InputTouchPhase.Moved, 20, 0)),
                classifier);
            Assert.That(moved.Owner, Is.EqualTo(DecorationGestureOwner.FunctionalSurface));
            Assert.That(moved.FunctionalSurfaceDragRequested, Is.True);
            Assert.That(moved.SceneDragRequested, Is.False);
            Assert.That(moved.CameraPanRequested, Is.False);
            var canceled = fixture.Router.ProcessFrame(
                Frame(3, Point(19, 30, 10, InputTouchPhase.Canceled)),
                classifier);

            fixture.Controller.RouteTouchResultForActiveMode(canceled);

            Assert.That(fixture.Router.Owner, Is.EqualTo(DecorationGestureOwner.None));
            Assert.That(fixture.Session.ActivePreview, Is.Null);
            Assert.That(fixture.Scenario.Functional.PickUpPoints.Single().Address,
                Is.EqualTo(Address("slot.0")));
            Assert.That(fixture.PickUpIndicators.CurrentPreview, Is.Null);
            Assert.That(fixture.PickUpIndicators.TryGet(pointId, out var restored), Is.True);
            Assert.That(restored.activeInHierarchy, Is.True);
            Assert.That(ActiveDirectChildCount(fixture.PickUpRoot.transform), Is.EqualTo(1));
            fixture.Controller.enabled = false;

            yield return null;

            Assert.That(fixture.PickUpRoot.transform.childCount, Is.EqualTo(1));
            Assert.That(ActiveDirectChildCount(fixture.PickUpRoot.transform), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator Controller_RealMountedView_ExitDisableAndDestroyRestoreWithoutGhosts()
        {
            foreach (var cleanup in new[]
                     {
                         ControllerCleanup.Exit,
                         ControllerCleanup.Disable,
                         ControllerCleanup.Destroy
                     })
            {
                using var fixture = new FunctionalViewHarness();
                const string mountedId = "90909090909090909090909090909090";
                Assert.That(fixture.Scenario.Functional.PlaceMounted(new SurfaceMountedInstance(
                    mountedId,
                    RegisterDefinitionId,
                    Address("slot.0"),
                    FurnitureRotation.Degrees0)).Succeeded, Is.True);
                fixture.RebuildConfirmedViews();
                Assert.That(fixture.MountedRegistry.TryGet(mountedId, out var confirmed), Is.True);
                Assert.That(confirmed.activeInHierarchy, Is.True);
                Assert.That(fixture.Controller.TryBeginExistingFunctionalSurfacePreview(
                    FunctionalSurfacePreviewKind.MountedEquipment,
                    mountedId), Is.True);
                Assert.That(confirmed.activeSelf, Is.False);
                Assert.That(fixture.MountedPreview.CurrentGhost.activeInHierarchy, Is.True);

                var classifier = new FixedClassifier(new DecorationTouchHit(
                    DecorationTouchHitKind.FunctionalSurface,
                    targetId: mountedId,
                    functionalSurfaceAddress: Address("slot.1")));
                fixture.Router.ProcessFrame(
                    Frame(1, Point(29, 10, 10, InputTouchPhase.Began)),
                    classifier);
                var moved = fixture.Router.ProcessFrame(
                    Frame(2, Point(29, 30, 10, InputTouchPhase.Moved, 20, 0)),
                    classifier);
                Assert.That(moved.Owner, Is.EqualTo(DecorationGestureOwner.FunctionalSurface));
                Assert.That(moved.SceneDragRequested, Is.False);
                Assert.That(moved.CameraPanRequested, Is.False);
                Set(fixture.Controller, "cleanupRequired", true);

                switch (cleanup)
                {
                    case ControllerCleanup.Exit:
                        fixture.Controller.ExitDecorationMode();
                        fixture.Controller.enabled = false;
                        break;
                    case ControllerCleanup.Disable:
                        fixture.Controller.enabled = false;
                        break;
                    case ControllerCleanup.Destroy:
                        UnityEngine.Object.DestroyImmediate(fixture.Controller);
                        break;
                }

                Assert.That(fixture.Router.Owner, Is.EqualTo(DecorationGestureOwner.None), cleanup.ToString());
                Assert.That(fixture.Session.ActivePreview, Is.Null, cleanup.ToString());
                Assert.That(fixture.Scenario.Functional.MountedInstances.Single().Address,
                    Is.EqualTo(Address("slot.0")), cleanup.ToString());
                Assert.That(fixture.MountedPreview.CurrentGhost, Is.Null, cleanup.ToString());
                Assert.That(fixture.MountedPreview.CurrentFootprint, Is.Null, cleanup.ToString());
                Assert.That(fixture.MountedRegistry.TryGet(mountedId, out var restored), Is.True);
                Assert.That(restored.activeInHierarchy, Is.True, cleanup.ToString());
                Assert.That(ActiveDirectChildCount(fixture.PreviewRoot.transform), Is.Zero,
                    cleanup.ToString());
                Assert.That(ActiveDirectChildCount(fixture.MountedRoot.transform), Is.EqualTo(1),
                    cleanup.ToString());

                yield return null;

                Assert.That(fixture.PreviewRoot.transform.childCount, Is.Zero, cleanup.ToString());
                Assert.That(fixture.MountedRoot.transform.childCount, Is.EqualTo(1), cleanup.ToString());
            }
        }

        [UnityTest]
        public IEnumerator Controller_SupportPreviewProjectsBoundContentWithoutPublishingUntilConfirm()
        {
            using var fixture = new FunctionalViewHarness();
            Assert.That(fixture.Scenario.Cafe.PlaceFurniture(FurnitureInstance.Restore(
                OtherSupportInstanceId,
                SupportDefinitionId,
                new GridPosition(0, 7),
                FurnitureRotation.Degrees0)).Succeeded, Is.True);
            var mountedA = new SurfaceMountedInstance(
                PreviewMountedIdA,
                RegisterDefinitionId,
                Address("slot.0"),
                FurnitureRotation.Degrees90);
            var mountedB = new SurfaceMountedInstance(
                PreviewMountedIdB,
                CoffeeMachineDefinitionId,
                Address("slot.1"),
                FurnitureRotation.Degrees270);
            var mountedOther = new SurfaceMountedInstance(
                PreviewMountedOtherId,
                RegisterDefinitionId,
                new SurfaceSlotAddress(OtherSupportInstanceId, "slot.4"),
                FurnitureRotation.Degrees0);
            var pickUpA = new PickUpPointInstance(PreviewPickUpIdA, Address("slot.2"));
            var pickUpB = new PickUpPointInstance(PreviewPickUpIdB, Address("slot.3"));
            Assert.That(fixture.Scenario.Functional.PlaceMounted(mountedA).Succeeded, Is.True);
            Assert.That(fixture.Scenario.Functional.PlaceMounted(mountedB).Succeeded, Is.True);
            Assert.That(fixture.Scenario.Functional.PlaceMounted(mountedOther).Succeeded, Is.True);
            Assert.That(fixture.Scenario.Functional.PlacePickUp(pickUpA).Succeeded, Is.True);
            Assert.That(fixture.Scenario.Functional.PlacePickUp(pickUpB).Succeeded, Is.True);
            fixture.RebuildAllConfirmedViews();
            Assert.That(fixture.MountedRegistry.TryGet(PreviewMountedIdA, out var mountedViewA), Is.True);
            Assert.That(fixture.MountedRegistry.TryGet(PreviewMountedIdB, out var mountedViewB), Is.True);
            Assert.That(fixture.MountedRegistry.TryGet(PreviewMountedOtherId, out var otherView), Is.True);
            Assert.That(fixture.PickUpIndicators.TryGet(PreviewPickUpIdA, out var pickUpViewA), Is.True);
            Assert.That(fixture.PickUpIndicators.TryGet(PreviewPickUpIdB, out var pickUpViewB), Is.True);
            var otherPosition = otherView.transform.position;

            Invoke(fixture.Controller, "HandleFurnitureBegan", SupportInstanceId);

            Assert.That(fixture.OrdinarySession.ActivePreview, Is.Not.Null);
            Assert.That(fixture.Scenario.Cafe.FurnitureInstances.Single(item =>
                item.InstanceId == SupportInstanceId).Position, Is.EqualTo(new GridPosition(0, 0)));
            Assert.That(fixture.Scenario.Functional.MountedInstances.Single(item =>
                item.InstanceId == PreviewMountedIdA), Is.SameAs(mountedA));
            Assert.That(fixture.Scenario.Functional.PickUpPoints.Single(item =>
                item.InstanceId == PreviewPickUpIdA), Is.SameAs(pickUpA));

            Invoke(fixture.Controller, "ApplyPreviewMove", new GridPosition(2, 2));
            AssertProjectedViews(
                mountedViewA,
                mountedViewB,
                pickUpViewA,
                pickUpViewB,
                new Vector3(2.5f, 0.72f, 2.5f),
                new Vector3(3.5f, 0.72f, 2.5f),
                new Vector3(4.5f, 0.72f, 2.5f),
                new Vector3(5.5f, 0.72f, 2.5f));
            Assert.That(otherView.transform.position, Is.EqualTo(otherPosition));

            Invoke(fixture.Controller, "HandleRotateRequested");
            AssertProjectedViews(
                mountedViewA, mountedViewB, pickUpViewA, pickUpViewB,
                new Vector3(4.5f, 0.72f, 4.5f),
                new Vector3(4.5f, 0.72f, 3.5f),
                new Vector3(4.5f, 0.72f, 2.5f),
                new Vector3(4.5f, 0.72f, 1.5f));
            Invoke(fixture.Controller, "HandleRotateRequested");
            AssertProjectedViews(
                mountedViewA, mountedViewB, pickUpViewA, pickUpViewB,
                new Vector3(6.5f, 0.72f, 2.5f),
                new Vector3(5.5f, 0.72f, 2.5f),
                new Vector3(4.5f, 0.72f, 2.5f),
                new Vector3(3.5f, 0.72f, 2.5f));
            Invoke(fixture.Controller, "HandleRotateRequested");
            AssertProjectedViews(
                mountedViewA, mountedViewB, pickUpViewA, pickUpViewB,
                new Vector3(4.5f, 0.72f, 0.5f),
                new Vector3(4.5f, 0.72f, 1.5f),
                new Vector3(4.5f, 0.72f, 2.5f),
                new Vector3(4.5f, 0.72f, 3.5f));

            Invoke(fixture.Controller, "ApplyPreviewMove", new GridPosition(0, 7));
            Assert.That(fixture.OrdinarySession.ActivePreview.PlacementResult.Succeeded, Is.False);
            AssertProjectedViews(
                mountedViewA, mountedViewB, pickUpViewA, pickUpViewB,
                new Vector3(0.5f, 0.72f, 3.5f),
                new Vector3(0.5f, 0.72f, 4.5f),
                new Vector3(0.5f, 0.72f, 5.5f),
                new Vector3(0.5f, 0.72f, 6.5f));
            Invoke(fixture.Controller, "HandleConfirmRequested");
            Assert.That(fixture.OrdinarySession.ActivePreview, Is.Not.Null,
                "Invalid support Preview must remain uncommitted.");
            Assert.That(fixture.Scenario.Cafe.FurnitureInstances.Single(item =>
                item.InstanceId == SupportInstanceId).Position, Is.EqualTo(new GridPosition(0, 0)));

            fixture.Controller.CancelActivePreview();
            Assert.That(fixture.OrdinarySession.ActivePreview, Is.Null);
            Assert.That(fixture.MountedRegistry.TryGet(PreviewMountedIdA, out var restoredMounted), Is.True);
            Assert.That(fixture.PickUpIndicators.TryGet(PreviewPickUpIdA, out var restoredPickUp), Is.True);
            Assert.That(restoredMounted.transform.position,
                Is.EqualTo(new Vector3(0.5f, 0.72f, 0.5f)));
            Assert.That(restoredPickUp.transform.position,
                Is.EqualTo(new Vector3(2.5f, 0.72f, 0.5f)));
            Set(fixture.Controller, "isOpen", false);
            yield return null;
            Assert.That(fixture.FurniturePreviewRoot.transform.childCount, Is.Zero);
            Set(fixture.Controller, "isOpen", true);
            Assert.That(ActiveDirectChildCount(fixture.MountedRoot.transform), Is.EqualTo(3));
            Assert.That(ActiveDirectChildCount(fixture.PickUpRoot.transform), Is.EqualTo(2));

            Invoke(fixture.Controller, "HandleFurnitureBegan", SupportInstanceId);
            Invoke(fixture.Controller, "ApplyPreviewMove", new GridPosition(2, 2));
            Invoke(fixture.Controller, "HandleRotateRequested");
            Invoke(fixture.Controller, "HandleConfirmRequested");
            Assert.That(fixture.OrdinarySession.ActivePreview, Is.Null);
            var committedSupport = fixture.Scenario.Cafe.FurnitureInstances.Single(item =>
                item.InstanceId == SupportInstanceId);
            Assert.That(committedSupport.Position, Is.EqualTo(new GridPosition(4, 0)));
            Assert.That(committedSupport.Rotation, Is.EqualTo(FurnitureRotation.Degrees90));
            Assert.That(fixture.Scenario.Functional.MountedInstances.Single(item =>
                item.InstanceId == PreviewMountedIdA).Address, Is.EqualTo(Address("slot.0")));
            Assert.That(fixture.Scenario.Functional.MountedInstances.Single(item =>
                item.InstanceId == PreviewMountedIdA).Rotation,
                Is.EqualTo(FurnitureRotation.Degrees90));
            Assert.That(fixture.Scenario.Functional.PickUpPoints.Select(item => item.InstanceId),
                Is.EquivalentTo(new[] { PreviewPickUpIdA, PreviewPickUpIdB }));
            Assert.That(ActiveDirectChildCount(fixture.MountedRoot.transform), Is.EqualTo(3));
            Assert.That(ActiveDirectChildCount(fixture.PickUpRoot.transform), Is.EqualTo(2));

            Invoke(fixture.Controller, "HandleFurnitureBegan", SupportInstanceId);
            Invoke(fixture.Controller, "ApplyPreviewMove", new GridPosition(2, 2));
            Set(fixture.Controller, "cleanupRequired", true);
            fixture.Controller.enabled = false;
            Assert.That(fixture.OrdinarySession.ActivePreview, Is.Null);
            Assert.That(fixture.MountedRegistry.TryGet(PreviewMountedIdA, out var disableRestored), Is.True);
            Assert.That(disableRestored.transform.position,
                Is.EqualTo(new Vector3(4.5f, 0.72f, 4.5f)));
            Assert.That(fixture.PickUpIndicators.TryGet(PreviewPickUpIdA, out _), Is.False,
                "Decoration-only Pick-up indicators must hide when controller disable exits Decoration.");
            Assert.That(fixture.FurniturePreview.CurrentPreviewTransform, Is.Null);
        }

        [UnityTest]
        public IEnumerator Controller_SupportPreviewExitAndDisableRestoreConfirmedProjection()
        {
            foreach (var cleanup in new[]
                     {
                         ControllerCleanup.Exit,
                         ControllerCleanup.Disable
                     })
            {
                using var fixture = new FunctionalViewHarness();
                var mounted = new SurfaceMountedInstance(
                    PreviewMountedIdA,
                    RegisterDefinitionId,
                    Address("slot.0"),
                    FurnitureRotation.Degrees0);
                var pickUp = new PickUpPointInstance(
                    PreviewPickUpIdA,
                    Address("slot.1"));
                Assert.That(fixture.Scenario.Functional.PlaceMounted(mounted).Succeeded, Is.True);
                Assert.That(fixture.Scenario.Functional.PlacePickUp(pickUp).Succeeded, Is.True);
                fixture.RebuildAllConfirmedViews();
                Assert.That(fixture.MountedRegistry.TryGet(
                    PreviewMountedIdA,
                    out var originalMounted), Is.True);
                Assert.That(fixture.PickUpIndicators.TryGet(
                    PreviewPickUpIdA,
                    out var originalPickUp), Is.True);
                AssertWorldPosition(originalMounted, new Vector3(0.5f, 0.72f, 0.5f));
                AssertWorldPosition(originalPickUp, new Vector3(1.5f, 0.72f, 0.5f));

                Invoke(fixture.Controller, "HandleFurnitureBegan", SupportInstanceId);
                Invoke(fixture.Controller, "ApplyPreviewMove", new GridPosition(2, 2));
                AssertWorldPosition(originalMounted, new Vector3(2.5f, 0.72f, 2.5f));
                AssertWorldPosition(originalPickUp, new Vector3(3.5f, 0.72f, 2.5f));
                Set(fixture.Controller, "cleanupRequired", true);

                if (cleanup == ControllerCleanup.Exit)
                {
                    fixture.Controller.ExitDecorationMode();
                }
                else
                {
                    fixture.Controller.enabled = false;
                }

                Assert.That(fixture.OrdinarySession.ActivePreview, Is.Null, cleanup.ToString());
                Assert.That(fixture.Scenario.Cafe.FurnitureInstances.Single(item =>
                    item.InstanceId == SupportInstanceId).Position,
                    Is.EqualTo(new GridPosition(0, 0)), cleanup.ToString());
                Assert.That(fixture.Scenario.Functional.MountedInstances.Single(item =>
                    item.InstanceId == PreviewMountedIdA), Is.SameAs(mounted), cleanup.ToString());
                Assert.That(fixture.Scenario.Functional.PickUpPoints.Single(item =>
                    item.InstanceId == PreviewPickUpIdA), Is.SameAs(pickUp), cleanup.ToString());
                Assert.That(fixture.MountedRegistry.TryGet(
                    PreviewMountedIdA,
                    out var restoredMounted), Is.True, cleanup.ToString());
                AssertWorldPosition(restoredMounted, new Vector3(0.5f, 0.72f, 0.5f));
                Assert.That(fixture.PickUpIndicators.TryGet(PreviewPickUpIdA, out _), Is.False,
                    $"{cleanup}: Decoration-only Pick-up indicators must hide on exit.");
                Assert.That(ActiveDirectChildCount(fixture.MountedRoot.transform), Is.EqualTo(1));
                Assert.That(ActiveDirectChildCount(fixture.PickUpRoot.transform), Is.Zero);

                yield return null;

                Assert.That(fixture.FurniturePreviewRoot.transform.childCount, Is.Zero);
                Assert.That(fixture.MountedRoot.transform.childCount, Is.EqualTo(1));
                Assert.That(fixture.PickUpRoot.transform.childCount, Is.Zero);
            }
        }

        [Test]
        public void Controller_SupportPreviewSourceSwitchRestoresPreviousSupportBeforeProjectingNext()
        {
            using var fixture = new FunctionalViewHarness();
            Assert.That(fixture.Scenario.Cafe.PlaceFurniture(FurnitureInstance.Restore(
                OtherSupportInstanceId,
                SupportDefinitionId,
                new GridPosition(0, 7),
                FurnitureRotation.Degrees0)).Succeeded, Is.True);
            var firstMounted = new SurfaceMountedInstance(
                PreviewMountedIdA,
                RegisterDefinitionId,
                Address("slot.0"),
                FurnitureRotation.Degrees0);
            var otherMounted = new SurfaceMountedInstance(
                PreviewMountedOtherId,
                RegisterDefinitionId,
                new SurfaceSlotAddress(OtherSupportInstanceId, "slot.4"),
                FurnitureRotation.Degrees0);
            Assert.That(fixture.Scenario.Functional.PlaceMounted(firstMounted).Succeeded, Is.True);
            Assert.That(fixture.Scenario.Functional.PlaceMounted(otherMounted).Succeeded, Is.True);
            fixture.RebuildAllConfirmedViews();

            Invoke(fixture.Controller, "HandleFurnitureBegan", SupportInstanceId);
            Invoke(fixture.Controller, "ApplyPreviewMove", new GridPosition(2, 2));
            Assert.That(fixture.MountedRegistry.TryGet(PreviewMountedIdA, out var projectedFirst),
                Is.True);
            AssertWorldPosition(projectedFirst, new Vector3(2.5f, 0.72f, 2.5f));

            Invoke(fixture.Controller, "HandleFurnitureBegan", OtherSupportInstanceId);

            Assert.That(fixture.OrdinarySession.ActivePreview.SourceInstanceId,
                Is.EqualTo(OtherSupportInstanceId));
            Assert.That(fixture.MountedRegistry.TryGet(PreviewMountedIdA, out var restoredFirst),
                Is.True);
            AssertWorldPosition(restoredFirst, new Vector3(0.5f, 0.72f, 0.5f));
            Assert.That(fixture.MountedRegistry.TryGet(
                PreviewMountedOtherId,
                out var projectedOther), Is.True);
            Invoke(fixture.Controller, "ApplyPreviewMove", new GridPosition(2, 5));
            AssertWorldPosition(projectedOther, new Vector3(6.5f, 0.72f, 5.5f));
            AssertWorldPosition(restoredFirst, new Vector3(0.5f, 0.72f, 0.5f));
            Assert.That(fixture.Scenario.Cafe.FurnitureInstances.Single(item =>
                item.InstanceId == SupportInstanceId).Position, Is.EqualTo(new GridPosition(0, 0)));
            Assert.That(fixture.Scenario.Cafe.FurnitureInstances.Single(item =>
                item.InstanceId == OtherSupportInstanceId).Position, Is.EqualTo(new GridPosition(0, 7)));

            fixture.Controller.CancelActivePreview();
            Assert.That(fixture.MountedRegistry.TryGet(
                PreviewMountedOtherId,
                out var restoredOther), Is.True);
            AssertWorldPosition(restoredOther, new Vector3(4.5f, 0.72f, 7.5f));
        }

        [Test]
        public void FloorDecorationSession_WithSharedFunctionalLayout_BlocksOccupiedSupportStore()
        {
            var scenario = CreateScenario();
            var mounted = new SurfaceMountedInstance(
                "33333333333333333333333333333333",
                RegisterDefinitionId,
                Address("slot.0"),
                FurnitureRotation.Degrees0);
            Assert.That(scenario.Functional.PlaceMounted(mounted).Succeeded, Is.True);
            var floorSession = new DecorationSession(scenario.Cafe, scenario.Functional);
            floorSession.Enter();
            Assert.That(floorSession.BeginExisting(SupportInstanceId).Succeeded, Is.True);
            Assert.That(floorSession.BeginStoreConfirmation(), Is.True);

            var result = floorSession.ConfirmStore();

            Assert.That(result.FailureReason, Is.EqualTo(PlacementFailureReason.Blocked));
            Assert.That(floorSession.ActivePreview.StoreBlockerContentIds,
                Is.EqualTo(new[] { mounted.InstanceId }));
            Assert.That(scenario.Cafe.TryGetFurnitureInstance(SupportInstanceId, out _), Is.True);
            Assert.That(scenario.Functional.MountedInstances.Single(), Is.SameAs(mounted));
        }

        [Test]
        public void Controller_BlockedSupportStore_PreservesTopReadinessAndShowsLocalReasonWithExactDiagnostics()
        {
            using var fixture = new FunctionalViewHarness();
            var mounted = new SurfaceMountedInstance(
                "33333333333333333333333333333333",
                RegisterDefinitionId,
                Address("slot.0"),
                FurnitureRotation.Degrees0);
            var coffee = new SurfaceMountedInstance(
                "44444444444444444444444444444444",
                CoffeeMachineDefinitionId,
                Address("slot.1"),
                FurnitureRotation.Degrees0);
            var pickUp = new PickUpPointInstance(
                "55555555555555555555555555555555",
                Address("slot.2"));
            Assert.That(fixture.Scenario.Functional.PlaceMounted(mounted).Succeeded, Is.True);
            Assert.That(fixture.Scenario.Functional.PlaceMounted(coffee).Succeeded, Is.True);
            Assert.That(fixture.Scenario.Functional.PlacePickUp(pickUp).Succeeded, Is.True);
            fixture.RebuildAllConfirmedViews();

            var labelObject = new GameObject("ValidationLabel", typeof(RectTransform));
            labelObject.transform.SetParent(fixture.Root.transform, false);
            var label = labelObject.AddComponent<TextMeshProUGUI>();
            var validation = fixture.Root.AddComponent<ValidationMessageView>();
            validation.Configure(label);
            Set(fixture.Controller, "validationMessageView", validation);
            var editingObject = new GameObject("EditingLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
            editingObject.transform.SetParent(fixture.Root.transform, false);
            var editingLabel = editingObject.GetComponent<TMP_Text>();
            Set(fixture.ActionBar, "feedbackLabel", editingLabel);
            Assert.That(fixture.OrdinarySession.BeginExisting(SupportInstanceId).Succeeded, Is.True);
            Assert.That(fixture.OrdinarySession.BeginStoreConfirmation(), Is.True);

            Invoke(fixture.Controller, "HandleStoreConfirmRequested");

            Assert.That(fixture.OrdinarySession.State,
                Is.EqualTo(DecorationSessionState.EditingExistingFurniture));
            Assert.That(validation.IsVisible, Is.False, "Store failure must not publish a readiness report.");
            Assert.That(fixture.Controller.EditingDiagnosticIds,
                Is.EqualTo(new[] { mounted.InstanceId, coffee.InstanceId, pickUp.InstanceId }),
                "The controller must snapshot blockers before dismissing the Store confirmation.");
            Assert.That(editingLabel.text, Does.Contain("收银机（1）"),
                "Players need a readable list of the contents they must remove first.");
            Assert.That(editingLabel.text, Does.Contain("咖啡机（1）"));
            Assert.That(editingLabel.text, Does.Contain("取餐点（1）"));
            Assert.That(label.text, Is.Empty);
            foreach (var internalId in new[]
                     { mounted.InstanceId, coffee.InstanceId, pickUp.InstanceId, SupportInstanceId })
            {
                Assert.That(editingLabel.text, Does.Not.Contain(internalId));
            }
            Assert.That(fixture.Scenario.Cafe.TryGetFurnitureInstance(SupportInstanceId, out _), Is.True);
            Assert.That(fixture.Scenario.Functional.MountedInstances, Is.EqualTo(new[] { mounted, coffee }));
            Assert.That(fixture.Scenario.Functional.PickUpPoints, Is.EqualTo(new[] { pickUp }));
            var storeReason = editingLabel.text;
            Invoke(fixture.Controller, "SubscribeViewEvents");
            fixture.Catalogue.ShowCatalogue();
            fixture.Catalogue.ShowCollapsedHandle();
            Assert.That(editingLabel.text, Is.EqualTo(storeReason),
                "The original collapse handle must preserve the Store failure just like Return to Editing.");
            Assert.That(fixture.Controller.EditingDiagnosticIds,
                Is.EqualTo(new[] { mounted.InstanceId, coffee.InstanceId, pickUp.InstanceId }));
            fixture.ConfigureStoreModal();
            Invoke(fixture.Controller, "HandleStoreRequested");
            Assert.That(fixture.StoreModal.IsOpen, Is.True);
            fixture.StoreCancel.onClick.Invoke();
            Assert.That(editingLabel.text, Is.EqualTo(storeReason),
                "Dismissing a repeated Store prompt must preserve the previous failed-operation reason.");
            Assert.That(fixture.Controller.EditingDiagnosticIds,
                Is.EqualTo(new[] { mounted.InstanceId, coffee.InstanceId, pickUp.InstanceId }));
        }

        [TestCase(DecorationCatalogueItemKind.CashRegister, RegisterDefinitionId)]
        [TestCase(DecorationCatalogueItemKind.CoffeeMachine, CoffeeMachineDefinitionId)]
        public void MountedEquipment_NewAndExisting_MoveRotateConfirmCancelAndStore(
            DecorationCatalogueItemKind kind,
            string definitionId)
        {
            var root = new GameObject("Phase8MountedLifecycle");
            try
            {
                var scenario = CreateScenario();
                var controller = root.AddComponent<DecorationModeController>();
                Set(controller, "functionalSurfaceSession",
                    new FunctionalSurfaceDecorationSession(scenario.Functional));

                Assert.That(controller.TryBeginFunctionalSurfacePreview(
                    kind, definitionId, Address("slot.0")), Is.True);
                Assert.That(controller.TryMoveFunctionalSurfacePreview(Address("slot.1")), Is.True);
                Assert.That(controller.TryRotateFunctionalSurfacePreview(), Is.True);
                controller.CancelFunctionalSurfacePreview();
                Assert.That(scenario.Functional.MountedInstances, Is.Empty);

                Assert.That(controller.TryBeginFunctionalSurfacePreview(
                    kind, definitionId, Address("slot.0")), Is.True);
                Assert.That(controller.TryConfirmFunctionalSurfacePreview(), Is.True);
                var confirmed = scenario.Functional.MountedInstances.Single();

                Assert.That(controller.TryBeginExistingFunctionalSurfacePreview(
                    FunctionalSurfacePreviewKind.MountedEquipment,
                    confirmed.InstanceId), Is.True);
                Assert.That(controller.TryMoveFunctionalSurfacePreview(Address("slot.1")), Is.True);
                Assert.That(controller.TryRotateFunctionalSurfacePreview(), Is.True);
                controller.CancelFunctionalSurfacePreview();
                Assert.That(scenario.Functional.MountedInstances.Single().Address,
                    Is.EqualTo(Address("slot.0")));
                Assert.That(scenario.Functional.MountedInstances.Single().Rotation,
                    Is.EqualTo(FurnitureRotation.Degrees0));

                Assert.That(controller.TryBeginExistingFunctionalSurfacePreview(
                    FunctionalSurfacePreviewKind.MountedEquipment,
                    confirmed.InstanceId), Is.True);
                Assert.That(controller.TryMoveFunctionalSurfacePreview(Address("slot.1")), Is.True);
                Assert.That(controller.TryRotateFunctionalSurfacePreview(), Is.True);
                Assert.That(controller.TryConfirmFunctionalSurfacePreview(), Is.True);
                Assert.That(scenario.Functional.MountedInstances.Single().Address,
                    Is.EqualTo(Address("slot.1")));
                Assert.That(scenario.Functional.MountedInstances.Single().Rotation,
                    Is.EqualTo(FurnitureRotation.Degrees90));

                Assert.That(controller.TryBeginExistingFunctionalSurfacePreview(
                    FunctionalSurfacePreviewKind.MountedEquipment,
                    confirmed.InstanceId), Is.True);
                Assert.That(controller.TryStoreFunctionalSurfacePreview(), Is.True);
                Assert.That(scenario.Functional.MountedInstances, Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void InvalidOccupiedFloorAndMissingSlot_CannotConfirmAndNeverMutateConfirmedState()
        {
            var root = new GameObject("Phase8InvalidCandidates");
            try
            {
                var scenario = CreateScenario();
                Assert.That(scenario.Functional.PlacePickUp(new PickUpPointInstance(
                    "44444444444444444444444444444444",
                    Address("slot.0"))).Succeeded, Is.True);
                var controller = root.AddComponent<DecorationModeController>();
                Set(controller, "functionalSurfaceSession",
                    new FunctionalSurfaceDecorationSession(scenario.Functional));

                Assert.That(controller.TryBeginFunctionalSurfacePreview(
                    DecorationCatalogueItemKind.CashRegister,
                    RegisterDefinitionId,
                    Address("slot.0")), Is.True);
                Assert.That(controller.ActiveFunctionalSurfacePreview.Validation.FailureReason,
                    Is.EqualTo(FunctionalSurfacePlacementFailureReason.SlotOccupied));
                Assert.That(controller.TryConfirmFunctionalSurfacePreview(), Is.False);
                Assert.That(scenario.Functional.MountedInstances, Is.Empty);
                Assert.That(scenario.Functional.PickUpPoints, Has.Count.EqualTo(1));
                controller.CancelFunctionalSurfacePreview();

                Assert.That(controller.TryBeginFunctionalSurfacePreview(
                    DecorationCatalogueItemKind.CashRegister,
                    RegisterDefinitionId,
                    new SurfaceSlotAddress(SupportInstanceId, "slot.missing")), Is.True);
                Assert.That(controller.ActiveFunctionalSurfacePreview.Validation.FailureReason,
                    Is.EqualTo(FunctionalSurfacePlacementFailureReason.MissingSurfaceSlot));
                Assert.That(controller.TryConfirmFunctionalSurfacePreview(), Is.False);
                Assert.That(scenario.Functional.MountedInstances, Is.Empty);
                controller.CancelFunctionalSurfacePreview();

                Assert.That(controller.TryBeginFunctionalSurfacePreview(
                    DecorationCatalogueItemKind.CashRegister,
                    RegisterDefinitionId,
                    Address("slot.1")), Is.True);
                var router = new DecorationTouchRouter(2f, 0f);
                var crossing = new SequencedClassifier(
                    new DecorationTouchHit(
                        DecorationTouchHitKind.FunctionalSurface,
                        targetId: controller.ActiveFunctionalSurfacePreview.InstanceId,
                        functionalSurfaceAddress: Address("slot.1")),
                    new DecorationTouchHit(
                        DecorationTouchHitKind.FloorGrid,
                        floorPosition: new GridPosition(3, 3)));
                router.ProcessFrame(Frame(5, Point(5, 10, 10, InputTouchPhase.Began)), crossing);
                var overFloor = router.ProcessFrame(
                    Frame(6, Point(5, 20, 10, InputTouchPhase.Moved, 10, 0)), crossing);
                controller.RouteTouchResultForActiveMode(overFloor);
                Assert.That(controller.ActiveFunctionalSurfacePreview.Validation.FailureReason,
                    Is.EqualTo(FunctionalSurfacePlacementFailureReason.InvalidSurfaceSlotAddress));
                Assert.That(controller.TryConfirmFunctionalSurfacePreview(), Is.False);
                Assert.That(scenario.Functional.MountedInstances, Is.Empty);
                Assert.That(scenario.Functional.PickUpPoints.Single().Address,
                    Is.EqualTo(Address("slot.0")));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [TestCase(DecorationModeKind.Furniture)]
        [TestCase(DecorationModeKind.Floor)]
        [TestCase(DecorationModeKind.Wall)]
        [TestCase(DecorationModeKind.WallDecor)]
        public void TabSwitch_FunctionalPreviewDiscardsOnlyWhenDestinationChanges(
            DecorationModeKind requestedMode)
        {
            var root = new GameObject("Phase8ModeSwitchCancel");
            try
            {
                var scenario = CreateScenario();
                var point = new PickUpPointInstance(
                    "55555555555555555555555555555555",
                    Address("slot.0"));
                Assert.That(scenario.Functional.PlacePickUp(point).Succeeded, Is.True);
                var controller = root.AddComponent<DecorationModeController>();
                Set(controller, "functionalSurfaceSession",
                    new FunctionalSurfaceDecorationSession(scenario.Functional));
                Assert.That(controller.TryBeginExistingFunctionalSurfacePreview(
                    FunctionalSurfacePreviewKind.PickUpPoint, point.InstanceId), Is.True);
                Assert.That(controller.TryMoveFunctionalSurfacePreview(Address("slot.1")), Is.True);

                var preview = controller.ActiveFunctionalSurfacePreview;
                Assert.That(controller.TryChangeMode(requestedMode), Is.True);

                Assert.That(controller.ActiveMode, Is.EqualTo(requestedMode));
                if (requestedMode == DecorationModeKind.Furniture)
                    Assert.That(controller.ActiveFunctionalSurfacePreview, Is.SameAs(preview));
                else
                    Assert.That(controller.ActiveFunctionalSurfacePreview, Is.Null);
                Assert.That(scenario.Functional.PickUpPoints.Single().Address,
                    Is.EqualTo(Address("slot.0")));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TabSwitch_FunctionalPreviewRestoresConfirmedViewsWithoutPublishingReadiness(
            [Values(DecorationCatalogueItemKind.CashRegister, DecorationCatalogueItemKind.CoffeeMachine,
                DecorationCatalogueItemKind.PickUpPoint)] DecorationCatalogueItemKind kind,
            [Values(false, true)] bool existing,
            [Values(false, true)] bool invalid)
        {
            using var fixture = new FunctionalViewHarness();
            if (kind == DecorationCatalogueItemKind.PickUpPoint) fixture.BeginPickUpPreview(existing);
            else fixture.BeginMountedPreview(kind,
                kind == DecorationCatalogueItemKind.CashRegister ? RegisterDefinitionId : CoffeeMachineDefinitionId,
                existing);
            var instanceId = fixture.Controller.ActiveFunctionalSurfacePreview.InstanceId;
            fixture.Controller.TryMoveFunctionalSurfacePreview(invalid ? default : Address("slot.1"));
            var version = fixture.Runtime.ReadinessVersion;
            var report = fixture.Runtime.CurrentReadiness;
            var furniture = fixture.Scenario.Cafe.FurnitureInstances.ToArray();

            Assert.That(fixture.Controller.TryChangeMode(DecorationModeKind.Wall), Is.True);

            Assert.That(fixture.Controller.ActiveFunctionalSurfacePreview, Is.Null);
            Assert.That(fixture.MountedPreview.CurrentGhost, Is.Null);
            Assert.That(fixture.PickUpIndicators.CurrentPreview, Is.Null);
            Assert.That(fixture.Runtime.ReadinessVersion, Is.EqualTo(version));
            Assert.That(fixture.Runtime.CurrentReadiness, Is.SameAs(report));
            Assert.That(fixture.Scenario.Cafe.FurnitureInstances, Is.EqualTo(furniture));
            Assert.That(fixture.Scenario.Functional.MountedInstances.Count + fixture.Scenario.Functional.PickUpPoints.Count,
                Is.EqualTo(existing ? 1 : 0));
            if (existing)
            {
                if (kind == DecorationCatalogueItemKind.PickUpPoint)
                {
                    Assert.That(fixture.Scenario.Functional.PickUpPoints.Single().Address, Is.EqualTo(Address("slot.0")));
                    Assert.That(fixture.PickUpIndicators.TryGet(instanceId, out var restored), Is.True);
                    Assert.That(restored.activeSelf, Is.True);
                }
                else
                {
                    Assert.That(fixture.Scenario.Functional.MountedInstances.Single().Address, Is.EqualTo(Address("slot.0")));
                    Assert.That(fixture.MountedRegistry.TryGet(instanceId, out var restored), Is.True);
                    Assert.That(restored.activeSelf, Is.True);
                }
            }
        }

        [Test]
        public void TabSwitch_SupportPreviewRestoresBoundEquipmentAndPickUpWithoutPublishingReadiness()
        {
            using var fixture = new FunctionalViewHarness();
            var register = new SurfaceMountedInstance(PreviewMountedIdA, RegisterDefinitionId,
                Address("slot.0"), FurnitureRotation.Degrees90);
            var coffeeMachine = new SurfaceMountedInstance(PreviewMountedIdB, CoffeeMachineDefinitionId,
                Address("slot.1"), FurnitureRotation.Degrees270);
            var pickUp = new PickUpPointInstance(PreviewPickUpIdA, Address("slot.2"));
            Assert.That(fixture.Scenario.Functional.PlaceMounted(register).Succeeded, Is.True);
            Assert.That(fixture.Scenario.Functional.PlaceMounted(coffeeMachine).Succeeded, Is.True);
            Assert.That(fixture.Scenario.Functional.PlacePickUp(pickUp).Succeeded, Is.True);
            fixture.RebuildAllConfirmedViews();
            ConfigureRuntime(fixture.Runtime, fixture.Scenario);
            var support = fixture.Scenario.Cafe.FurnitureInstances.Single();
            var readiness = fixture.Runtime.CurrentReadiness;
            var version = fixture.Runtime.ReadinessVersion;
            Assert.That(fixture.MountedRegistry.TryGet(PreviewMountedIdA, out var registerView), Is.True);
            Assert.That(fixture.MountedRegistry.TryGet(PreviewMountedIdB, out var coffeeView), Is.True);
            Assert.That(fixture.PickUpIndicators.TryGet(PreviewPickUpIdA, out var pickUpView), Is.True);
            var positions = new[] { registerView.transform.position, coffeeView.transform.position,
                pickUpView.transform.position };
            var rotations = new[] { registerView.transform.rotation, coffeeView.transform.rotation,
                pickUpView.transform.rotation };

            // Move and rotate the support's real preview: bound views must actually leave their saved positions.
            // 先真实移动、旋转柜台预览，再确认切 Tab 能恢复设备与取餐点的已保存投影。
            Invoke(fixture.Controller, "HandleFurnitureBegan", SupportInstanceId);
            Invoke(fixture.Controller, "ApplyPreviewMove", new GridPosition(2, 2));
            Invoke(fixture.Controller, "HandleRotateRequested");
            Assert.That(fixture.OrdinarySession.ActivePreview, Is.Not.Null);
            Assert.That(registerView.transform.position, Is.Not.EqualTo(positions[0]));
            Assert.That(coffeeView.transform.position, Is.Not.EqualTo(positions[1]));
            Assert.That(pickUpView.transform.position, Is.Not.EqualTo(positions[2]));

            Assert.That(fixture.Controller.TryChangeMode(DecorationModeKind.Wall), Is.True);

            Assert.That(fixture.OrdinarySession.ActivePreview, Is.Null);
            Assert.That(fixture.FurniturePreview.CurrentPreviewTransform, Is.Null);
            Assert.That(fixture.Scenario.Cafe.FurnitureInstances.Single(), Is.SameAs(support));
            Assert.That(support.Position, Is.EqualTo(new GridPosition(0, 0)));
            Assert.That(support.Rotation, Is.EqualTo(FurnitureRotation.Degrees0));
            Assert.That(fixture.Scenario.Functional.MountedInstances, Is.EquivalentTo(new[] { register, coffeeMachine }));
            Assert.That(fixture.Scenario.Functional.MountedInstances.Single(item => item.InstanceId == PreviewMountedIdA),
                Is.SameAs(register));
            Assert.That(fixture.Scenario.Functional.MountedInstances.Single(item => item.InstanceId == PreviewMountedIdB),
                Is.SameAs(coffeeMachine));
            Assert.That(register.Address, Is.EqualTo(Address("slot.0")));
            Assert.That(coffeeMachine.Address, Is.EqualTo(Address("slot.1")));
            Assert.That(register.Rotation, Is.EqualTo(FurnitureRotation.Degrees90));
            Assert.That(coffeeMachine.Rotation, Is.EqualTo(FurnitureRotation.Degrees270));
            Assert.That(fixture.Scenario.Functional.PickUpPoints.Single(), Is.SameAs(pickUp));
            Assert.That(pickUp.Address, Is.EqualTo(Address("slot.2")));
            Assert.That(fixture.MountedRegistry.TryGet(PreviewMountedIdA, out var restoredRegister), Is.True);
            Assert.That(fixture.MountedRegistry.TryGet(PreviewMountedIdB, out var restoredCoffee), Is.True);
            Assert.That(fixture.PickUpIndicators.TryGet(PreviewPickUpIdA, out var restoredPickUp), Is.True);
            var restored = new[] { restoredRegister, restoredCoffee, restoredPickUp };
            for (var index = 0; index < restored.Length; index++)
            {
                Assert.That(restored[index].activeSelf, Is.True);
                Assert.That(restored[index].transform.position, Is.EqualTo(positions[index]));
                Assert.That(restored[index].transform.rotation, Is.EqualTo(rotations[index]));
            }
            Assert.That(ActiveDirectChildCount(fixture.MountedRoot.transform), Is.EqualTo(2));
            Assert.That(ActiveDirectChildCount(fixture.PickUpRoot.transform), Is.EqualTo(1));
            Assert.That(fixture.Runtime.ReadinessVersion, Is.EqualTo(version));
            Assert.That(fixture.Runtime.CurrentReadiness, Is.SameAs(readiness));
        }

        [TestCase(InputTouchPhase.Ended)]
        [TestCase(InputTouchPhase.Canceled)]
        [TestCase(InputTouchPhase.None)]
        public void TabSwitch_ControllerConsumesOldAllUpBeforeAcceptingNextTouch(InputTouchPhase releasePhase)
        {
            using var fixture = new FunctionalViewHarness();
            fixture.BeginPickUpPreview(false);
            var source = new ControlledTouchSource();
            Set(fixture.Controller, "touchSource", source);
            var screen = fixture.Camera.WorldToScreenPoint(new Vector3(6.5f, 0f, 5.5f));
            source.SetFrame(1, Point(71, screen.x, screen.y, InputTouchPhase.Began));
            Invoke(fixture.Controller, "Update");
            Assert.That(fixture.Router.PrimaryTouchId, Is.EqualTo(71));
            Assert.That(fixture.Router.Owner, Is.EqualTo(DecorationGestureOwner.Camera));

            // UI onClick can switch tabs before Controller.Update sees this release frame.
            // 模拟同一帧先点击 Tab、后读取旧手指松开；必须经过 controller 的 device-family gate。
            source.SetFrame(2, releasePhase == InputTouchPhase.None
                ? Array.Empty<DecorationTouchPoint>()
                : new[] { Point(71, screen.x, screen.y, releasePhase) });
            Assert.That(fixture.Controller.TryChangeMode(DecorationModeKind.Wall), Is.True);
            Assert.That(fixture.Router.IsSuppressingUntilAllTouchesUp, Is.True);
            Invoke(fixture.Controller, "Update");

            Assert.That(fixture.Router.IsSuppressingUntilAllTouchesUp, Is.False,
                "The terminal-only/empty frame must reach the router even when it contains no new Began.");
            Assert.That(fixture.Router.Owner, Is.EqualTo(DecorationGestureOwner.None));
            Assert.That(fixture.Controller.ActiveFunctionalSurfacePreview, Is.Null);
            source.SetFrame(3, Point(72, screen.x, screen.y, InputTouchPhase.Began));
            Invoke(fixture.Controller, "Update");
            Assert.That(fixture.Router.PrimaryTouchId, Is.EqualTo(72),
                "The very next gesture must be accepted, not swallowed to retire the previous touch.");
            Assert.That(fixture.Router.Owner, Is.EqualTo(DecorationGestureOwner.Camera));
        }

        [Test]
        public void TabSwitch_AbortsOldPointerUntilAllTouchesRelease()
        {
            using var fixture = new FunctionalViewHarness();
            fixture.BeginPickUpPreview(false);
            var classifier = new FixedClassifier(new DecorationTouchHit(
                DecorationTouchHitKind.FunctionalSurface, functionalSurfaceAddress: Address("slot.0")));
            fixture.Router.ProcessFrame(Frame(1, Point(41, 10, 10, InputTouchPhase.Began)), classifier);
            fixture.Controller.RouteTouchResultForActiveMode(fixture.Router.ProcessFrame(
                Frame(2, Point(41, 30, 10, InputTouchPhase.Moved, 20, 0)), classifier));
            Assert.That(fixture.Router.IsDragging, Is.True);

            Assert.That(fixture.Controller.TryChangeMode(DecorationModeKind.Wall), Is.True);

            Assert.That(fixture.Router.Owner, Is.EqualTo(DecorationGestureOwner.None));
            Assert.That(fixture.Router.IsDragging, Is.False);
            Assert.That(fixture.Router.IsSuppressingUntilAllTouchesUp, Is.True);
            // A new finger must not promote while the old drag is still held.
            // 旧拖拽尚未松手时，不让另一个 Began 接管新模式。
            var held = fixture.Router.ProcessFrame(Frame(3,
                Point(41, 30, 10, InputTouchPhase.Stationary),
                Point(42, 50, 10, InputTouchPhase.Began)), classifier);
            Assert.That(held.Owner, Is.EqualTo(DecorationGestureOwner.None));
            var released = fixture.Router.ProcessFrame(Frame(4,
                Point(41, 30, 10, InputTouchPhase.Ended),
                Point(42, 50, 10, InputTouchPhase.Ended)), classifier);
            Assert.That(released.TapReleased, Is.False);
            Assert.That(released.FunctionalSurfaceDragRequested, Is.False);
            Assert.That(fixture.Router.IsSuppressingUntilAllTouchesUp, Is.False);
            Assert.That(fixture.Router.ProcessFrame(Frame(5,
                Point(43, 10, 10, InputTouchPhase.Began)), classifier).Owner,
                Is.EqualTo(DecorationGestureOwner.FunctionalSurface));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Controller_FunctionalPreview_RejectsOrdinaryFurnitureBegins(bool existing)
        {
            using var fixture = new FunctionalViewHarness();
            Assert.That(fixture.Controller.TryBeginFunctionalSurfacePreview(
                DecorationCatalogueItemKind.CashRegister, RegisterDefinitionId,
                Address("slot.0")), Is.True);
            var preview = fixture.Controller.ActiveFunctionalSurfacePreview;
            var version = fixture.Runtime.ReadinessVersion;

            if (existing)
            {
                Invoke(fixture.Controller, "HandleFurnitureBegan", SupportInstanceId);
            }
            else
            {
                Invoke(fixture.Controller, "HandleCatalogueSelected", fixture.SupportAsset);
            }

            Assert.That(fixture.OrdinarySession.ActivePreview, Is.Null,
                "An active functional preview must keep exclusive transaction ownership.");
            Assert.That(fixture.Controller.ActiveFunctionalSurfacePreview, Is.SameAs(preview));
            Assert.That(fixture.FurniturePreview.CurrentPreviewTransform, Is.Null);
            Assert.That(fixture.ActionBar.IsVisible, Is.True);
            Assert.That(fixture.Scenario.Functional.MountedInstances, Is.Empty);
            Assert.That(fixture.Runtime.ReadinessVersion, Is.EqualTo(version));
        }

        [TestCase(DecorationCatalogueItemKind.CashRegister, RegisterDefinitionId)]
        [TestCase(DecorationCatalogueItemKind.CoffeeMachine, CoffeeMachineDefinitionId)]
        [TestCase(DecorationCatalogueItemKind.PickUpPoint, null)]
        public void Controller_FunctionalPreviewBegin_CompactsCatalogueAndKeepsActionsAvailable(
            DecorationCatalogueItemKind kind, string definitionId)
        {
            using var fixture = new FunctionalViewHarness();
            fixture.Catalogue.ShowCatalogue();
            fixture.Catalogue.SetSheetState(DecorationSheetState.Expanded, false);

            Assert.That(fixture.Controller.TryBeginFunctionalSurfacePreview(
                kind, definitionId, Address("slot.0")), Is.True);

            Assert.That(fixture.Catalogue.SheetState,
                Is.EqualTo(DecorationSheetState.CompactPreview));
            Assert.That(fixture.Catalogue.AreCategoryRowsVisible, Is.False);
            Assert.That(fixture.ActionBar.IsVisible, Is.True);
            Assert.That(fixture.ActionBar.VisibleActionLabels, Does.Contain("Cancel"));
            Assert.That(fixture.ActionBar.VisibleActionLabels, Does.Contain("Confirm"));
        }

        [UnityTest]
        public IEnumerator Controller_SurfaceDragAcrossUi_PreservesSlotHeightAndResumesOnTable()
        {
            foreach (var kind in new[] { DecorationCatalogueItemKind.CashRegister,
                DecorationCatalogueItemKind.CoffeeMachine, DecorationCatalogueItemKind.PickUpPoint })
            foreach (var existing in new[] { false, true })
                yield return AssertSurfaceDragAcrossUi(kind, existing);
        }

        private IEnumerator AssertSurfaceDragAcrossUi(
            DecorationCatalogueItemKind kind, bool existing)
        {
            using var h = new FunctionalViewHarness();
            Set(h.Controller, "sanitizedFurnitureHoverHeight", .35f);
            if (kind == DecorationCatalogueItemKind.PickUpPoint) h.BeginPickUpPreview(existing);
            else h.BeginMountedPreview(kind,
                kind == DecorationCatalogueItemKind.CashRegister ? RegisterDefinitionId : CoffeeMachineDefinitionId, existing);
            GameObject Visual() => kind == DecorationCatalogueItemKind.PickUpPoint
                ? h.PickUpIndicators.CurrentPreview.transform.Find("InvertedSquarePyramid").gameObject
                : h.MountedPreview.CurrentGhost;
            var initialPosition = Visual().transform.position;
            var readiness = h.Runtime.CurrentReadiness;
            var version = h.Runtime.ReadinessVersion;
            h.RouteMountedPointer(new Vector3(.5f, .72f, .5f), InputTouchPhase.Began, 1);

            // Real UI raycast, not a fabricated hit / 使用真实 UI，覆盖手指进入按钮区域的路径。
            if (EventSystem.current == null)
            {
                var events = new GameObject("DragUiEventSystem", typeof(EventSystem));
                events.transform.SetParent(h.Root.transform, false);
            }
            var canvas = new GameObject("DragUiCanvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            canvas.transform.SetParent(h.Root.transform, false);
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var blocker = new GameObject("DragUiButton", typeof(RectTransform), typeof(Image), typeof(Button));
            blocker.transform.SetParent(canvas.transform, false);
            var rect = blocker.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(.9f, .9f);
            rect.sizeDelta = new Vector2(60, 60);
            // This harness routes input explicitly; no live input source runs during UI setup.
            // fixture 手动路由输入，等 UI 注册这一帧不读取硬件输入。
            Set(h.Controller, "isOpen", false);
            yield return null;
            Set(h.Controller, "isOpen", true);
            Canvas.ForceUpdateCanvases();
            var uiPoint = RectTransformUtility.WorldToScreenPoint(null, rect.position);
            var classifier = (IDecorationTouchHitClassifier)h.Controller;
            Assert.That(classifier.ClassifyCurrent(71, uiPoint).Kind, Is.EqualTo(DecorationTouchHitKind.Ui));
            var drag = h.Router.ProcessFrame(Frame(2, Point(71, uiPoint.x, uiPoint.y, InputTouchPhase.Moved)), classifier);
            h.Controller.RouteTouchResultForActiveMode(drag);

            Assert.That(drag.Owner, Is.EqualTo(DecorationGestureOwner.FunctionalSurface));
            Assert.That(h.Controller.ActiveFunctionalSurfacePreview.Address, Is.EqualTo(Address("slot.0")),
                "Crossing UI is not leaving the Counter; keep the last support binding.");
            AssertWorldPosition(Visual(), initialPosition);
            Assert.That(h.Controller.ActiveFunctionalSurfacePreview.CanConfirm, Is.True);
            Assert.That(h.Runtime.CurrentReadiness, Is.SameAs(readiness));
            Assert.That(h.Runtime.ReadinessVersion, Is.EqualTo(version));
            blocker.SetActive(false);
            h.RouteMountedPointer(new Vector3(2.5f, .72f, .5f), InputTouchPhase.Moved, 3);
            Assert.That(h.Controller.ActiveFunctionalSurfacePreview.Address, Is.EqualTo(Address("slot.2")));
            AssertWorldPosition(Visual(), initialPosition + new Vector3(2, 0, 0));
            h.Controller.CancelFunctionalSurfacePreview();
        }

        [TestCase(FurnitureRotation.Degrees0, 2.5f, .5f)]
        [TestCase(FurnitureRotation.Degrees90, .5f, 2.5f)]
        public void Controller_SurfaceDrag_TableCornerBeyondSnapRadius_StaysAtSlotHeight(
            FurnitureRotation rotation, float centerX, float centerZ)
        {
            using var h = new FunctionalViewHarness();
            Assert.That(h.Scenario.Cafe.RotateFurniture(SupportInstanceId, rotation).Succeeded, Is.True);
            h.RebuildAllConfirmedViews();
            Set(h.Controller, "sanitizedFurnitureHoverHeight", .35f);
            h.Camera.pixelRect = new Rect(0, 0, 1600, 1200);
            h.Camera.orthographicSize = 2f;
            h.Camera.transform.position = new Vector3(centerX, 10, centerZ);
            h.BeginMountedPreview(DecorationCatalogueItemKind.CashRegister, RegisterDefinitionId, false);
            Assert.That(h.Controller.TryMoveFunctionalSurfacePreview(Address("slot.2")), Is.True);
            var center = new Vector3(centerX, .72f, centerZ);
            var corner = center + new Vector3(.44f, 0, .44f);
            Assert.That(Vector2.Distance(h.Camera.WorldToScreenPoint(center), h.Camera.WorldToScreenPoint(corner)),
                Is.GreaterThan(72), "Exercise an actual tabletop corner outside the old circular snap radius.");
            h.RouteMountedPointer(center, InputTouchPhase.Began, 1);
            var drag = h.RouteMountedPointer(corner, InputTouchPhase.Moved, 2);
            Assert.That(drag.FunctionalSurfaceDragRequested, Is.True);
            Assert.That(drag.CurrentHit.Kind, Is.EqualTo(DecorationTouchHitKind.FunctionalSurface));
            Assert.That(drag.CurrentHit.FunctionalSurfaceAddress, Is.EqualTo(Address("slot.2")),
                "The tabletop must be hit, not merely freeze because floor projection failed.");
            Assert.That(h.Controller.ActiveFunctionalSurfacePreview.Address, Is.EqualTo(Address("slot.2")));
            AssertWorldPosition(h.MountedPreview.CurrentGhost, new Vector3(centerX, 1.07f, centerZ));
            Assert.That(h.Controller.ActiveFunctionalSurfacePreview.CanConfirm, Is.True);
            Assert.That(h.Scenario.Functional.MountedInstances, Is.Empty);
        }

        [TestCase(DecorationCatalogueItemKind.CashRegister, RegisterDefinitionId, false)]
        [TestCase(DecorationCatalogueItemKind.CashRegister, RegisterDefinitionId, true)]
        [TestCase(DecorationCatalogueItemKind.CoffeeMachine, CoffeeMachineDefinitionId, false)]
        [TestCase(DecorationCatalogueItemKind.CoffeeMachine, CoffeeMachineDefinitionId, true)]
        [TestCase(DecorationCatalogueItemKind.PickUpPoint, null, false)]
        [TestCase(DecorationCatalogueItemKind.PickUpPoint, null, true)]
        public void Controller_FunctionalStoreModal_OnlyConfirmedButtonRemovesAndPublishes(
            DecorationCatalogueItemKind kind, string definitionId, bool confirm)
        {
            using var fixture = new FunctionalViewHarness();
            fixture.ConfigureStoreModal();
            Assert.That(fixture.Controller.TryBeginFunctionalSurfacePreview(
                kind, definitionId, Address("slot.0")), Is.True);
            var instanceId = fixture.Controller.ActiveFunctionalSurfacePreview.InstanceId;
            Assert.That(fixture.Controller.TryConfirmFunctionalSurfacePreview(), Is.True);
            Assert.That(fixture.Controller.TryBeginExistingFunctionalSurfacePreview(
                kind == DecorationCatalogueItemKind.PickUpPoint
                    ? FunctionalSurfacePreviewKind.PickUpPoint
                    : FunctionalSurfacePreviewKind.MountedEquipment,
                instanceId), Is.True);
            Assert.That(fixture.Controller.TryMoveFunctionalSurfacePreview(Address("slot.2")),
                Is.True);
            var preview = fixture.Controller.ActiveFunctionalSurfacePreview;
            var version = fixture.Runtime.ReadinessVersion;
            var report = fixture.Runtime.CurrentReadiness;

            Invoke(fixture.Controller, "HandleStoreRequested");

            Assert.That(fixture.StoreModal.IsOpen, Is.True,
                "Store must open the shared confirmation modal before removing content.");
            Assert.That(fixture.Controller.ActiveFunctionalSurfacePreview, Is.SameAs(preview));
            Assert.That(fixture.Runtime.CurrentReadiness, Is.SameAs(report));
            Assert.That(fixture.Runtime.ReadinessVersion, Is.EqualTo(version));
            Assert.That(fixture.Scenario.Functional.MountedInstances.Count
                + fixture.Scenario.Functional.PickUpPoints.Count, Is.EqualTo(1));

            if (confirm)
            {
                fixture.StoreConfirm.onClick.Invoke();
                fixture.StoreConfirm.onClick.Invoke();
                Assert.That(fixture.Controller.ActiveFunctionalSurfacePreview, Is.Null);
                Assert.That(fixture.Scenario.Functional.MountedInstances, Is.Empty);
                Assert.That(fixture.Scenario.Functional.PickUpPoints, Is.Empty);
                Assert.That(fixture.Runtime.ReadinessVersion, Is.EqualTo(version + 1));
                Assert.That(fixture.Runtime.CurrentReadiness, Is.Not.SameAs(report));
                Assert.That(fixture.ActionBar.IsVisible, Is.False);
            }
            else
            {
                fixture.StoreCancel.onClick.Invoke();
                Assert.That(fixture.Controller.ActiveFunctionalSurfacePreview, Is.SameAs(preview));
                Assert.That(fixture.Controller.ActiveFunctionalSurfacePreview.Address,
                    Is.EqualTo(Address("slot.2")));
                var confirmedAddress = kind == DecorationCatalogueItemKind.PickUpPoint
                    ? fixture.Scenario.Functional.PickUpPoints.Single().Address
                    : fixture.Scenario.Functional.MountedInstances.Single().Address;
                Assert.That(confirmedAddress, Is.EqualTo(Address("slot.0")));
                Assert.That(fixture.Runtime.CurrentReadiness, Is.SameAs(report));
                Assert.That(fixture.Runtime.ReadinessVersion, Is.EqualTo(version));
                Assert.That(fixture.Catalogue.SheetState,
                    Is.EqualTo(DecorationSheetState.CompactPreview));
                Assert.That(fixture.ActionBar.IsVisible, Is.True);
            }
            Assert.That(fixture.StoreModal.IsOpen, Is.False);
        }

        [Test]
        public void Controller_ExistingInvalidPickUp_BeginsCompactRecoverablePreview()
        {
            using var fixture = new FunctionalViewHarness();
            Assert.That(fixture.Controller.TryBeginFunctionalSurfacePreview(
                DecorationCatalogueItemKind.PickUpPoint, null, Address("slot.0")), Is.True);
            var instanceId = fixture.Controller.ActiveFunctionalSurfacePreview.InstanceId;
            Assert.That(fixture.Controller.TryConfirmFunctionalSurfacePreview(), Is.True);
            fixture.Scenario.Cafe.AddReservation(new LayoutReservation(
                "blocked.pickup-neighbour", LayoutReservationType.Blocked,
                new GridPosition(0, 1), new GridSize(1, 1)));
            fixture.Catalogue.ShowCatalogue();
            fixture.Catalogue.SetSheetState(DecorationSheetState.Expanded, false);
            var report = fixture.Runtime.CurrentReadiness;
            var version = fixture.Runtime.ReadinessVersion;

            Assert.That(fixture.Controller.TryBeginExistingFunctionalSurfacePreview(
                FunctionalSurfacePreviewKind.PickUpPoint, instanceId), Is.True,
                "Existing invalid content must still enter an editable recovery preview.");

            Assert.That(fixture.Controller.ActiveFunctionalSurfacePreview.CanConfirm, Is.False);
            Assert.That(fixture.Controller.ActiveFunctionalSurfacePreview.Validation.FailureReason,
                Is.EqualTo(FunctionalSurfacePlacementFailureReason.NoValidInteractionAnchor));
            Assert.That(fixture.Catalogue.SheetState,
                Is.EqualTo(DecorationSheetState.CompactPreview));
            Assert.That(fixture.ActionBar.IsVisible, Is.True);
            Assert.That(fixture.Runtime.CurrentReadiness, Is.SameAs(report));
            Assert.That(fixture.Runtime.ReadinessVersion, Is.EqualTo(version));
            Assert.That(fixture.Controller.TryMoveFunctionalSurfacePreview(Address("slot.1")), Is.True);
            Assert.That(fixture.Scenario.Functional.PickUpPoints.Single().Address,
                Is.EqualTo(Address("slot.0")));
            Assert.That(fixture.Controller.TryConfirmFunctionalSurfacePreview(), Is.True);
            Assert.That(fixture.Scenario.Functional.PickUpPoints.Single().Address,
                Is.EqualTo(Address("slot.1")));
        }

        [Test]
        public void Controller_FunctionalStoreModal_RetainsExclusivePreviewUntilDismissed()
        {
            using var fixture = new FunctionalViewHarness();
            fixture.ConfigureStoreModal();
            Assert.That(fixture.Controller.TryBeginFunctionalSurfacePreview(
                DecorationCatalogueItemKind.PickUpPoint, null, Address("slot.0")), Is.True);
            var instanceId = fixture.Controller.ActiveFunctionalSurfacePreview.InstanceId;
            Assert.That(fixture.Controller.TryConfirmFunctionalSurfacePreview(), Is.True);
            Assert.That(fixture.Controller.TryBeginExistingFunctionalSurfacePreview(
                FunctionalSurfacePreviewKind.PickUpPoint, instanceId), Is.True);
            var preview = fixture.Controller.ActiveFunctionalSurfacePreview;
            var version = fixture.Runtime.ReadinessVersion;

            Invoke(fixture.Controller, "HandleStoreRequested");
            Invoke(fixture.Controller, "HandleStoreRequested");
            Assert.That(fixture.Controller.TryBeginFunctionalSurfacePreview(
                DecorationCatalogueItemKind.CashRegister, RegisterDefinitionId,
                Address("slot.1")), Is.False);
            Assert.That(fixture.Controller.TryBeginExistingFunctionalSurfacePreview(
                FunctionalSurfacePreviewKind.PickUpPoint, instanceId), Is.False);
            Invoke(fixture.Controller, "HandleCatalogueSelected", fixture.SupportAsset);
            Invoke(fixture.Controller, "HandleFurnitureBegan", SupportInstanceId);
            Assert.That(fixture.Controller.TryChangeMode(DecorationModeKind.Wall), Is.False);

            Assert.That(fixture.StoreModal.IsOpen, Is.True);
            Assert.That(fixture.Controller.ActiveFunctionalSurfacePreview, Is.SameAs(preview));
            Assert.That(fixture.OrdinarySession.ActivePreview, Is.Null);
            Assert.That(fixture.ActionBar.IsVisible, Is.False);
            Assert.That(fixture.Catalogue.SheetState, Is.EqualTo(DecorationSheetState.Hidden));
            Assert.That(fixture.Runtime.ReadinessVersion, Is.EqualTo(version));
            Assert.That(fixture.Scenario.Functional.PickUpPoints, Has.Count.EqualTo(1));

            fixture.StoreCancel.onClick.Invoke();
            Assert.That(fixture.StoreModal.IsOpen, Is.False);
            Assert.That(fixture.Controller.ActiveFunctionalSurfacePreview, Is.SameAs(preview));
            Invoke(fixture.Controller, "HandleStoreRequested");
            fixture.StoreConfirm.onClick.Invoke();
            Assert.That(fixture.Scenario.Functional.PickUpPoints, Is.Empty);
            Assert.That(fixture.Runtime.ReadinessVersion, Is.EqualTo(version + 1));
        }

        [TestCase(DecorationModeKind.Furniture, true)]
        [TestCase(DecorationModeKind.Furniture, false)]
        [TestCase(DecorationModeKind.Floor, false)]
        [TestCase(DecorationModeKind.Wall, false)]
        [TestCase(DecorationModeKind.WallDecor, false)]
        public void Controller_PinchZoom_ReachesCameraAcrossDecorationModes(
            DecorationModeKind mode, bool functionalPreview)
        {
            using var fixture = new FunctionalViewHarness();
            var settings = ScriptableObject.CreateInstance<CameraSettings>();
            try
            {
                settings.ZoomSpeed = 0.5f;
                var cameraController = fixture.Root.AddComponent<CafeCameraController>();
                cameraController.enabled = false;
                cameraController.Configure(fixture.Camera, settings, null);
                fixture.CameraDriver.Configure(cameraController);
                Set(fixture.Controller, "activeMode", mode);
                if (functionalPreview)
                {
                    Assert.That(fixture.Controller.TryBeginFunctionalSurfacePreview(
                        DecorationCatalogueItemKind.CashRegister, RegisterDefinitionId,
                        Address("slot.0")), Is.True);
                }
                var preview = fixture.Controller.ActiveFunctionalSurfacePreview;
                var classifier = new FixedClassifier(
                    new DecorationTouchHit(DecorationTouchHitKind.Scene));
                fixture.Router.ProcessFrame(Frame(1,
                    Point(1, 100, 100, InputTouchPhase.Began),
                    Point(2, 200, 100, InputTouchPhase.Began)), classifier);
                var pinch = fixture.Router.ProcessFrame(Frame(2,
                    Point(1, 80, 100, InputTouchPhase.Moved, -20, 0),
                    Point(2, 220, 100, InputTouchPhase.Moved, 20, 0)), classifier);
                Assert.That(pinch.PinchZoomRequested, Is.True);

                fixture.Controller.RouteTouchResultForActiveMode(pinch);

                Assert.That(fixture.Camera.orthographicSize, Is.EqualTo(4.5f).Within(0.001f));
                Assert.That(fixture.Controller.ActiveFunctionalSurfacePreview, Is.SameAs(preview));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void Controller_ActionBarUsesMountedAndPickUpMatricesForNewAndExisting()
        {
            var root = new GameObject("Phase8ActionBarMatrix");
            try
            {
                var scenario = CreateScenario();
                var actionBar = root.AddComponent<DecorationActionBarView>();
                var controller = root.AddComponent<DecorationModeController>();
                Set(controller, "actionBarView", actionBar);
                Set(controller, "functionalSurfaceSession",
                    new FunctionalSurfaceDecorationSession(scenario.Functional));

                Assert.That(controller.TryBeginFunctionalSurfacePreview(
                    DecorationCatalogueItemKind.CashRegister,
                    RegisterDefinitionId,
                    Address("slot.0")), Is.True);
                Assert.That(actionBar.VisibleActionLabels,
                    Is.EqualTo(new[] { "Cancel", "Rotate", "Confirm" }));
                controller.CancelFunctionalSurfacePreview();

                Assert.That(controller.TryBeginFunctionalSurfacePreview(
                    DecorationCatalogueItemKind.PickUpPoint,
                    null,
                    Address("slot.0")), Is.True);
                Assert.That(actionBar.VisibleActionLabels,
                    Is.EqualTo(new[] { "Cancel", "Confirm" }));
                Assert.That(controller.TryConfirmFunctionalSurfacePreview(), Is.True);
                var pointId = scenario.Functional.PickUpPoints.Single().InstanceId;
                Assert.That(controller.TryBeginExistingFunctionalSurfacePreview(
                    FunctionalSurfacePreviewKind.PickUpPoint,
                    pointId), Is.True);
                Assert.That(actionBar.VisibleActionLabels,
                    Is.EqualTo(new[] { "Store", "Cancel", "Confirm" }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [TestCase(DecorationCatalogueItemKind.CashRegister, RegisterDefinitionId, false)]
        [TestCase(DecorationCatalogueItemKind.CashRegister, RegisterDefinitionId, true)]
        [TestCase(DecorationCatalogueItemKind.CoffeeMachine, CoffeeMachineDefinitionId, false)]
        [TestCase(DecorationCatalogueItemKind.CoffeeMachine, CoffeeMachineDefinitionId, true)]
        [TestCase(DecorationCatalogueItemKind.PickUpPoint, null, false)]
        [TestCase(DecorationCatalogueItemKind.PickUpPoint, null, true)]
        public void Controller_FunctionalActionBar_FollowsRealPreviewAtStartMoveAndRotate(
            DecorationCatalogueItemKind kind,
            string definitionId,
            bool existing)
        {
            using var fixture = new FunctionalViewHarness();
            var previewKind = kind == DecorationCatalogueItemKind.PickUpPoint
                ? FunctionalSurfacePreviewKind.PickUpPoint
                : FunctionalSurfacePreviewKind.MountedEquipment;
            const string instanceId = "77777777777777777777777777777777";
            if (existing)
            {
                var placed = previewKind == FunctionalSurfacePreviewKind.PickUpPoint
                    ? fixture.Scenario.Functional.PlacePickUp(
                        new PickUpPointInstance(instanceId, Address("slot.0")))
                    : fixture.Scenario.Functional.PlaceMounted(
                        new SurfaceMountedInstance(
                            instanceId,
                            definitionId,
                            Address("slot.0"),
                            FurnitureRotation.Degrees0));
                Assert.That(placed.Succeeded, Is.True);
                fixture.RebuildConfirmedViews();
                Assert.That(fixture.Controller.TryBeginExistingFunctionalSurfacePreview(
                    previewKind,
                    instanceId), Is.True);
            }
            else
            {
                Assert.That(fixture.Controller.TryBeginFunctionalSurfacePreview(
                    kind,
                    definitionId,
                    Address("slot.0")), Is.True);
            }

            Canvas.ForceUpdateCanvases();
            var start = fixture.ActionBarRect.anchoredPosition;
            fixture.AssertActionBarTracksCurrentPreview();

            Assert.That(fixture.Controller.TryMoveFunctionalSurfacePreview(Address("slot.2")), Is.True);
            Canvas.ForceUpdateCanvases();
            var moved = fixture.ActionBarRect.anchoredPosition;
            Assert.That(Vector2.Distance(start, moved), Is.GreaterThan(10f),
                "Moving to another real Slot must move the floating ActionBar.");
            fixture.AssertActionBarTracksCurrentPreview();

            if (previewKind == FunctionalSurfacePreviewKind.MountedEquipment)
            {
                Assert.That(fixture.Controller.TryRotateFunctionalSurfacePreview(), Is.True);
                Canvas.ForceUpdateCanvases();
                fixture.AssertActionBarTracksCurrentPreview();
                Assert.That(fixture.ActionBar.VisibleActionLabels, Does.Contain("Rotate"));
            }
            else
            {
                Assert.That(fixture.Controller.TryRotateFunctionalSurfacePreview(), Is.False);
                Assert.That(fixture.ActionBar.VisibleActionLabels, Does.Not.Contain("Rotate"));
                Assert.That(fixture.ActionBarRect.anchoredPosition, Is.EqualTo(moved));
            }
        }

        [TestCase(DecorationCatalogueItemKind.CashRegister, RegisterDefinitionId, false)]
        [TestCase(DecorationCatalogueItemKind.CashRegister, RegisterDefinitionId, true)]
        [TestCase(DecorationCatalogueItemKind.CoffeeMachine, CoffeeMachineDefinitionId, false)]
        [TestCase(DecorationCatalogueItemKind.CoffeeMachine, CoffeeMachineDefinitionId, true)]
        public void Controller_MountedPreview_HoversOverSlotUntilConfirmed(
            DecorationCatalogueItemKind kind, string definitionId, bool existing)
        {
            using var fixture = new FunctionalViewHarness();
            Set(fixture.Controller, "sanitizedFurnitureHoverHeight", 0.35f);
            // Rotate the scene root to catch accidental world-Y-only hover offsets.
            fixture.Root.transform.SetPositionAndRotation(
                new Vector3(3f, 2f, -4f), Quaternion.Euler(0f, 25f, 20f));
            fixture.BeginMountedPreview(kind, definitionId, existing);
            var readiness = fixture.Runtime.CurrentReadiness;
            var version = fixture.Runtime.ReadinessVersion;
            var slot = fixture.Root.transform.TransformPoint(new Vector3(0.5f, 0.72f, 0.5f));
            var hovered = slot + fixture.Root.transform.up * 0.35f;

            AssertWorldPosition(fixture.MountedPreview.CurrentGhost, hovered);
            AssertWorldPosition(fixture.MountedPreview.CurrentFootprint, slot);
            Assert.That(fixture.Controller.TryRotateFunctionalSurfacePreview(), Is.True);
            AssertWorldPosition(fixture.MountedPreview.CurrentGhost, hovered);
            AssertWorldPosition(fixture.MountedPreview.CurrentFootprint, slot);
            Assert.That(Quaternion.Angle(fixture.MountedPreview.CurrentGhost.transform.rotation,
                fixture.Root.transform.rotation * Quaternion.Euler(0f, 90f, 0f)),
                Is.LessThan(0.01f));
            Assert.That(fixture.Runtime.CurrentReadiness, Is.SameAs(readiness));
            Assert.That(fixture.Runtime.ReadinessVersion, Is.EqualTo(version));

            var instanceId = fixture.Controller.ActiveFunctionalSurfacePreview.InstanceId;
            Assert.That(fixture.Controller.TryConfirmFunctionalSurfacePreview(), Is.True);
            Assert.That(fixture.MountedPreview.CurrentGhost, Is.Null);
            Assert.That(fixture.MountedPreview.CurrentFootprint, Is.Null);
            Assert.That(ActiveDirectChildCount(fixture.PreviewRoot.transform), Is.Zero);
            Assert.That(fixture.MountedRegistry.TryGet(instanceId, out var confirmed), Is.True);
            AssertWorldPosition(confirmed, slot);
            Assert.That(fixture.Scenario.Functional.MountedInstances.Single().Rotation,
                Is.EqualTo(FurnitureRotation.Degrees90));
        }

        [TestCase(DecorationCatalogueItemKind.CashRegister, RegisterDefinitionId, false)]
        [TestCase(DecorationCatalogueItemKind.CashRegister, RegisterDefinitionId, true)]
        [TestCase(DecorationCatalogueItemKind.CoffeeMachine, CoffeeMachineDefinitionId, false)]
        [TestCase(DecorationCatalogueItemKind.CoffeeMachine, CoffeeMachineDefinitionId, true)]
        public void Controller_MountedPreview_FloorDragRemainsVisibleInvalidAndCanBeGrabbedAgain(
            DecorationCatalogueItemKind kind, string definitionId, bool existing)
        {
            using var fixture = new FunctionalViewHarness();
            Set(fixture.Controller, "sanitizedFurnitureHoverHeight", 0.35f);
            // A slanted camera makes the elevated ghost project away from its ground cell.
            fixture.Camera.transform.position = new Vector3(7f, 10f, -7f);
            fixture.Camera.transform.LookAt(new Vector3(2f, 0f, 2f));
            fixture.Camera.orthographicSize = 4f;
            fixture.BeginMountedPreview(kind, definitionId, existing);
            var readiness = fixture.Runtime.CurrentReadiness;
            var version = fixture.Runtime.ReadinessVersion;
            var ground = new Vector3(3.5f, 0f, 3.5f);
            fixture.RouteMountedPointer(new Vector3(0.5f, 0.72f, 0.5f), InputTouchPhase.Began, 1);
            var drag = fixture.RouteMountedPointer(ground, InputTouchPhase.Moved, 2);

            Assert.That(drag.FunctionalSurfaceDragRequested, Is.True);
            Assert.That(fixture.MountedPreview.CurrentGhost, Is.Not.Null,
                "Dragging off a Counter must retain a visible, recoverable ground ghost.");
            AssertWorldPosition(fixture.MountedPreview.CurrentGhost, new Vector3(3.5f, 0.35f, 3.5f));
            AssertWorldPosition(fixture.MountedPreview.CurrentFootprint, ground);
            Assert.That(fixture.Controller.ActiveFunctionalSurfacePreview.CanConfirm, Is.False);
            Assert.That(fixture.Controller.TryConfirmFunctionalSurfacePreview(), Is.False);
            var color = new MaterialPropertyBlock();
            fixture.MountedPreview.CurrentFootprint.GetComponentInChildren<Renderer>()
                .GetPropertyBlock(color);
            var displayedColor = color.GetColor(Shader.PropertyToID("_BaseColor"));
            Assert.That(displayedColor.r, Is.EqualTo(0.92f).Within(0.00001f));
            Assert.That(displayedColor.g, Is.EqualTo(0.20f).Within(0.00001f));
            Assert.That(displayedColor.b, Is.EqualTo(0.22f).Within(0.00001f));
            Assert.That(displayedColor.a, Is.EqualTo(0.95f).Within(0.00001f));

            fixture.Controller.TryRotateFunctionalSurfacePreview();
            Assert.That(fixture.Controller.ActiveFunctionalSurfacePreview.Rotation,
                Is.EqualTo(FurnitureRotation.Degrees90));
            AssertWorldPosition(fixture.MountedPreview.CurrentGhost, new Vector3(3.5f, 0.35f, 3.5f));
            AssertWorldPosition(fixture.MountedPreview.CurrentFootprint, ground);
            fixture.RouteMountedPointer(ground, InputTouchPhase.Ended, 3);
            Assert.That(fixture.Router.Owner, Is.EqualTo(DecorationGestureOwner.None));

            var emptyScreen = fixture.Camera.WorldToScreenPoint(new Vector3(6.5f, 0f, 5.5f));
            Assert.That(((IDecorationTouchHitClassifier)fixture.Controller)
                .ClassifyBegan(72, emptyScreen).Kind, Is.EqualTo(DecorationTouchHitKind.Scene),
                "Empty ground must remain available to camera gestures.");

            var ghostCenter = fixture.MountedPreview.CurrentGhost
                .GetComponentInChildren<Renderer>().bounds.center;
            var grabbed = fixture.RouteMountedPointer(ghostCenter, InputTouchPhase.Began, 4);
            Assert.That(grabbed.Owner, Is.EqualTo(DecorationGestureOwner.FunctionalSurface),
                "A released ground ghost must own a fresh pointer sequence.");
            var returned = fixture.RouteMountedPointer(
                new Vector3(2.5f, 0.72f, 0.5f), InputTouchPhase.Moved, 5);
            Assert.That(returned.FunctionalSurfaceDragRequested, Is.True);
            Assert.That(fixture.Controller.ActiveFunctionalSurfacePreview.Address,
                Is.EqualTo(Address("slot.2")));
            Assert.That(fixture.Controller.ActiveFunctionalSurfacePreview.CanConfirm, Is.True);
            AssertWorldPosition(fixture.MountedPreview.CurrentGhost, new Vector3(2.5f, 1.07f, 0.5f));
            AssertWorldPosition(fixture.MountedPreview.CurrentFootprint, new Vector3(2.5f, 0.72f, 0.5f));
            Assert.That(fixture.Runtime.CurrentReadiness, Is.SameAs(readiness));
            Assert.That(fixture.Runtime.ReadinessVersion, Is.EqualTo(version));
            Assert.That(fixture.Scenario.Functional.MountedInstances.Count, Is.EqualTo(existing ? 1 : 0));
            if (existing)
            {
                Assert.That(fixture.Scenario.Functional.MountedInstances.Single().Address,
                    Is.EqualTo(Address("slot.0")));
                Assert.That(fixture.Scenario.Functional.MountedInstances.Single().Rotation,
                    Is.EqualTo(FurnitureRotation.Degrees0));
            }

            Assert.That(fixture.Controller.TryConfirmFunctionalSurfacePreview(), Is.True);
            var confirmed = fixture.Scenario.Functional.MountedInstances.Single();
            Assert.That(confirmed.Address, Is.EqualTo(Address("slot.2")));
            Assert.That(fixture.MountedRegistry.TryGet(confirmed.InstanceId, out var representation), Is.True);
            AssertWorldPosition(representation, new Vector3(2.5f, 0.72f, 0.5f));
            Assert.That(ActiveDirectChildCount(fixture.PreviewRoot.transform), Is.Zero);
        }

        [TestCase(DecorationCatalogueItemKind.CashRegister, RegisterDefinitionId)]
        [TestCase(DecorationCatalogueItemKind.CoffeeMachine, CoffeeMachineDefinitionId)]
        public void Controller_MountedPreview_FloorCancelRestoresSourceAndClearsFallback(
            DecorationCatalogueItemKind kind, string definitionId)
        {
            using var fixture = new FunctionalViewHarness();
            Set(fixture.Controller, "sanitizedFurnitureHoverHeight", 0.35f);
            fixture.BeginMountedPreview(kind, definitionId, existing: true);
            var instanceId = fixture.Controller.ActiveFunctionalSurfacePreview.InstanceId;
            var readiness = fixture.Runtime.CurrentReadiness;
            var version = fixture.Runtime.ReadinessVersion;
            fixture.RouteMountedPointer(new Vector3(0.5f, 0.72f, 0.5f), InputTouchPhase.Began, 1);
            fixture.RouteMountedPointer(new Vector3(3.5f, 0f, 3.5f), InputTouchPhase.Moved, 2);
            Assert.That(fixture.MountedPreview.CurrentGhost, Is.Not.Null);

            fixture.RouteMountedPointer(new Vector3(3.5f, 0f, 3.5f), InputTouchPhase.Canceled, 3);

            Assert.That(fixture.Controller.ActiveFunctionalSurfacePreview, Is.Null);
            Assert.That(fixture.MountedPreview.CurrentGhost, Is.Null);
            Assert.That(fixture.MountedPreview.CurrentFootprint, Is.Null);
            Assert.That(ActiveDirectChildCount(fixture.PreviewRoot.transform), Is.Zero);
            Assert.That(fixture.Runtime.CurrentReadiness, Is.SameAs(readiness));
            Assert.That(fixture.Runtime.ReadinessVersion, Is.EqualTo(version));
            Assert.That(fixture.MountedRegistry.TryGet(instanceId, out var restored), Is.True);
            Assert.That(restored.activeInHierarchy, Is.True);
            AssertWorldPosition(restored, new Vector3(0.5f, 0.72f, 0.5f));
            Assert.That(fixture.Controller.TryBeginFunctionalSurfacePreview(
                kind, definitionId, Address("slot.2")), Is.True);
            AssertWorldPosition(fixture.MountedPreview.CurrentGhost, new Vector3(2.5f, 1.07f, 0.5f));
        }

        [TestCase(DecorationCatalogueItemKind.CashRegister, RegisterDefinitionId)]
        [TestCase(DecorationCatalogueItemKind.CoffeeMachine, CoffeeMachineDefinitionId)]
        public void Controller_MountedPreview_NoCounterStartsVisibleInvalidGroundGhost(
            DecorationCatalogueItemKind kind, string definitionId)
        {
            using var fixture = new FunctionalViewHarness();
            Set(fixture.Controller, "sanitizedFurnitureHoverHeight", 0.35f);
            Assert.That(fixture.Scenario.Cafe.RemoveFurniture(SupportInstanceId).Succeeded, Is.True);
            fixture.RebuildAllConfirmedViews();
            var readiness = fixture.Runtime.CurrentReadiness;
            var version = fixture.Runtime.ReadinessVersion;

            Assert.That(fixture.SelectMountedCatalogueItem(kind, definitionId), Is.True);

            Assert.That(fixture.MountedPreview.CurrentGhost, Is.Not.Null,
                "Selecting equipment without a Counter must still show a recoverable preview.");
            AssertWorldPosition(fixture.MountedPreview.CurrentGhost, new Vector3(1.5f, 0.35f, 0.5f));
            AssertWorldPosition(fixture.MountedPreview.CurrentFootprint, new Vector3(1.5f, 0f, 0.5f));
            Assert.That(fixture.Controller.ActiveFunctionalSurfacePreview.CanConfirm, Is.False);
            Assert.That(fixture.Controller.TryConfirmFunctionalSurfacePreview(), Is.False);
            Assert.That(fixture.Scenario.Functional.MountedInstances, Is.Empty);
            Assert.That(fixture.Runtime.CurrentReadiness, Is.SameAs(readiness));
            Assert.That(fixture.Runtime.ReadinessVersion, Is.EqualTo(version));
            fixture.Controller.CancelFunctionalSurfacePreview();
            Assert.That(ActiveDirectChildCount(fixture.PreviewRoot.transform), Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Controller_PickUpPreview_HoversOnlyModelUntilConfirmed(bool existing)
        {
            using var fixture = new FunctionalViewHarness();
            Set(fixture.Controller, "sanitizedFurnitureHoverHeight", 0.35f);
            fixture.BeginPickUpPreview(existing);
            var preview = fixture.PickUpIndicators.CurrentPreview;
            AssertWorldPosition(preview.transform.Find("InvertedSquarePyramid").gameObject,
                new Vector3(0.7f, 1.37f, 0.5f));
            AssertWorldPosition(preview.transform.Find("Footprint").gameObject,
                new Vector3(0.5f, 0.74f, 0.5f));
            Assert.That(fixture.Controller.TryRotateFunctionalSurfacePreview(), Is.False);
            Assert.That(fixture.ActionBar.VisibleActionLabels, Does.Not.Contain("Rotate"));

            var instanceId = fixture.Controller.ActiveFunctionalSurfacePreview.InstanceId;
            Assert.That(fixture.Controller.TryConfirmFunctionalSurfacePreview(), Is.True);
            Assert.That(fixture.PickUpIndicators.CurrentPreview, Is.Null);
            Assert.That(fixture.PickUpIndicators.TryGet(instanceId, out var confirmed), Is.True);
            AssertWorldPosition(confirmed.transform.Find("InvertedSquarePyramid").gameObject,
                new Vector3(0.7f, 1.02f, 0.5f));
            AssertWorldPosition(confirmed.transform.Find("Footprint").gameObject,
                new Vector3(0.5f, 0.74f, 0.5f));
            Assert.That(ActiveDirectChildCount(fixture.PickUpRoot.transform), Is.EqualTo(1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Controller_PickUpPreview_FloorDragRemainsVisibleInvalidAndCanBeGrabbedAgain(bool existing)
        {
            using var fixture = new FunctionalViewHarness();
            Set(fixture.Controller, "sanitizedFurnitureHoverHeight", 0.35f);
            fixture.Camera.transform.position = new Vector3(7f, 10f, -7f);
            fixture.Camera.transform.LookAt(new Vector3(2f, 0f, 2f));
            fixture.Camera.orthographicSize = 4f;
            fixture.BeginPickUpPreview(existing);
            var readiness = fixture.Runtime.CurrentReadiness;
            var version = fixture.Runtime.ReadinessVersion;
            var ground = new Vector3(3.5f, 0f, 3.5f);
            fixture.RouteMountedPointer(new Vector3(0.5f, 0.72f, 0.5f), InputTouchPhase.Began, 1);
            var drag = fixture.RouteMountedPointer(ground, InputTouchPhase.Moved, 2);

            Assert.That(drag.FunctionalSurfaceDragRequested, Is.True);
            Assert.That(fixture.PickUpIndicators.CurrentPreview, Is.Not.Null,
                "Dragging a Pick-up Point off its Counter must keep a recoverable preview visible.");
            AssertWorldPosition(fixture.PickUpIndicators.CurrentPreview
                .transform.Find("InvertedSquarePyramid").gameObject, new Vector3(3.7f, 0.65f, 3.5f));
            AssertWorldPosition(fixture.PickUpIndicators.CurrentPreview
                .transform.Find("Footprint").gameObject, new Vector3(3.5f, 0.02f, 3.5f));
            Assert.That(fixture.Controller.ActiveFunctionalSurfacePreview.CanConfirm, Is.False);
            Assert.That(fixture.Controller.TryConfirmFunctionalSurfacePreview(), Is.False);
            Assert.That(fixture.Controller.TryRotateFunctionalSurfacePreview(), Is.False);
            var footprintColor = new MaterialPropertyBlock();
            fixture.PickUpIndicators.CurrentPreview.transform.Find("Footprint")
                .GetComponent<Renderer>().GetPropertyBlock(footprintColor);
            var displayed = footprintColor.GetColor(Shader.PropertyToID("_BaseColor"));
            Assert.That(displayed.r, Is.EqualTo(0.92f).Within(0.00001f));
            Assert.That(displayed.g, Is.EqualTo(0.20f).Within(0.00001f));
            Assert.That(displayed.b, Is.EqualTo(0.22f).Within(0.00001f));
            fixture.RouteMountedPointer(ground, InputTouchPhase.Ended, 3);
            Assert.That(fixture.Router.Owner, Is.EqualTo(DecorationGestureOwner.None));

            var emptyScreen = fixture.Camera.WorldToScreenPoint(new Vector3(6.5f, 0f, 5.5f));
            Assert.That(((IDecorationTouchHitClassifier)fixture.Controller)
                .ClassifyBegan(72, emptyScreen).Kind, Is.EqualTo(DecorationTouchHitKind.Scene),
                "Empty ground must stay available to camera gestures while a Pick-up preview exists.");
            var bodyCenter = fixture.PickUpIndicators.CurrentPreview
                .transform.Find("InvertedSquarePyramid").GetComponent<Renderer>().bounds.center;
            var grabbed = fixture.RouteMountedPointer(bodyCenter, InputTouchPhase.Began, 4);
            Assert.That(grabbed.Owner, Is.EqualTo(DecorationGestureOwner.FunctionalSurface));
            var returned = fixture.RouteMountedPointer(
                new Vector3(2.5f, 0.72f, 0.5f), InputTouchPhase.Moved, 5);
            Assert.That(returned.FunctionalSurfaceDragRequested, Is.True);
            Assert.That(fixture.Controller.ActiveFunctionalSurfacePreview.Address,
                Is.EqualTo(Address("slot.2")));
            Assert.That(fixture.Controller.ActiveFunctionalSurfacePreview.CanConfirm, Is.True);
            Assert.That(fixture.Runtime.CurrentReadiness, Is.SameAs(readiness));
            Assert.That(fixture.Runtime.ReadinessVersion, Is.EqualTo(version));
            Assert.That(fixture.Scenario.Functional.PickUpPoints.Count, Is.EqualTo(existing ? 1 : 0));
            if (existing)
                Assert.That(fixture.Scenario.Functional.PickUpPoints.Single().Address,
                    Is.EqualTo(Address("slot.0")));

            Assert.That(fixture.Controller.TryConfirmFunctionalSurfacePreview(), Is.True);
            var confirmed = fixture.Scenario.Functional.PickUpPoints.Single();
            Assert.That(confirmed.Address, Is.EqualTo(Address("slot.2")));
            Assert.That(fixture.PickUpIndicators.TryGet(confirmed.InstanceId, out var representation), Is.True);
            AssertWorldPosition(representation, new Vector3(2.5f, 0.72f, 0.5f));
            Assert.That(fixture.PickUpIndicators.CurrentPreview, Is.Null);
            Assert.That(ActiveDirectChildCount(fixture.PickUpRoot.transform), Is.EqualTo(1));
        }

        [Test]
        public void Controller_PickUpPreview_FloorCancelRestoresSourceAndClearsFallback()
        {
            using var fixture = new FunctionalViewHarness();
            Set(fixture.Controller, "sanitizedFurnitureHoverHeight", 0.35f);
            fixture.BeginPickUpPreview(existing: true);
            var instanceId = fixture.Controller.ActiveFunctionalSurfacePreview.InstanceId;
            var readiness = fixture.Runtime.CurrentReadiness;
            var version = fixture.Runtime.ReadinessVersion;
            fixture.RouteMountedPointer(new Vector3(0.5f, 0.72f, 0.5f), InputTouchPhase.Began, 1);
            fixture.RouteMountedPointer(new Vector3(3.5f, 0f, 3.5f), InputTouchPhase.Moved, 2);
            Assert.That(fixture.PickUpIndicators.CurrentPreview, Is.Not.Null);
            fixture.RouteMountedPointer(new Vector3(3.5f, 0f, 3.5f), InputTouchPhase.Canceled, 3);

            Assert.That(fixture.Controller.ActiveFunctionalSurfacePreview, Is.Null);
            Assert.That(fixture.PickUpIndicators.CurrentPreview, Is.Null);
            Assert.That(fixture.PickUpIndicators.TryGet(instanceId, out var restored), Is.True);
            Assert.That(restored.activeInHierarchy, Is.True);
            AssertWorldPosition(restored, new Vector3(0.5f, 0.72f, 0.5f));
            Assert.That(fixture.Runtime.CurrentReadiness, Is.SameAs(readiness));
            Assert.That(fixture.Runtime.ReadinessVersion, Is.EqualTo(version));
            Assert.That(ActiveDirectChildCount(fixture.PickUpRoot.transform), Is.EqualTo(1));
            Assert.That(fixture.Controller.TryBeginFunctionalSurfacePreview(
                DecorationCatalogueItemKind.PickUpPoint, null, Address("slot.2")), Is.True);
            AssertWorldPosition(fixture.PickUpIndicators.CurrentPreview
                .transform.Find("InvertedSquarePyramid").gameObject, new Vector3(2.7f, 1.37f, 0.5f));
        }

        [Test]
        public void Controller_PickUpPreview_NoCounterStartsVisibleInvalidGroundGhost()
        {
            using var fixture = new FunctionalViewHarness();
            Set(fixture.Controller, "sanitizedFurnitureHoverHeight", 0.35f);
            Assert.That(fixture.Scenario.Cafe.RemoveFurniture(SupportInstanceId).Succeeded, Is.True);
            fixture.RebuildAllConfirmedViews();
            var readiness = fixture.Runtime.CurrentReadiness;
            var version = fixture.Runtime.ReadinessVersion;

            Invoke(fixture.Controller, "HandlePickUpPointRequested");

            Assert.That(fixture.PickUpIndicators.CurrentPreview, Is.Not.Null,
                "A Pick-up catalogue request without a Counter must show an invalid ground preview.");
            AssertWorldPosition(fixture.PickUpIndicators.CurrentPreview
                .transform.Find("InvertedSquarePyramid").gameObject, new Vector3(1.7f, 0.65f, 0.5f));
            AssertWorldPosition(fixture.PickUpIndicators.CurrentPreview
                .transform.Find("Footprint").gameObject, new Vector3(1.5f, 0.02f, 0.5f));
            Assert.That(fixture.Controller.ActiveFunctionalSurfacePreview.CanConfirm, Is.False);
            Assert.That(fixture.Controller.TryConfirmFunctionalSurfacePreview(), Is.False);
            Assert.That(fixture.Scenario.Functional.PickUpPoints, Is.Empty);
            Assert.That(fixture.Runtime.CurrentReadiness, Is.SameAs(readiness));
            Assert.That(fixture.Runtime.ReadinessVersion, Is.EqualTo(version));
            fixture.Controller.CancelFunctionalSurfacePreview();
            Assert.That(ActiveDirectChildCount(fixture.PickUpRoot.transform), Is.Zero);
        }

        [Test]
        public void Controller_FurnitureCatalogueKeepsAllThreeRows_AndRoutesEquipmentAndPickUp()
        {
            var root = new GameObject("Phase8ControllerCatalogue", typeof(RectTransform));
            var registerAsset = CreateDefinitionAsset(
                RegisterDefinitionId,
                FurnitureFunctionType.CashRegister);
            var coffeeAsset = CreateDefinitionAsset(
                CoffeeMachineDefinitionId,
                FurnitureFunctionType.CoffeeMachine);
            try
            {
                var scenario = CreateScenario();
                var runtime = root.AddComponent<CafeLayoutRuntime>();
                ConfigureRuntime(runtime, scenario);
                var controller = root.AddComponent<DecorationModeController>();
                Set(controller, "layoutRuntime", runtime);
                Set(controller, "functionalSurfaceSession",
                    new FunctionalSurfaceDecorationSession(scenario.Functional));
                Set(controller, "isOpen", true);

                var content = new GameObject("CategoryContent", typeof(RectTransform));
                content.transform.SetParent(root.transform, false);
                var pickUpObject = new GameObject(
                    "PickUpPointButton", typeof(RectTransform), typeof(UnityEngine.UI.Button));
                pickUpObject.transform.SetParent(root.transform, false);
                var pickUpButton = pickUpObject.GetComponent<UnityEngine.UI.Button>();
                var catalogue = root.AddComponent<DecorationCatalogueView>();
                Set(catalogue, "categoryContent", content.GetComponent<RectTransform>());
                Set(catalogue, "pickUpPointButton", pickUpButton);
                catalogue.Configure(new UiPointerBoundary(), new UiTransitionRunner(() => true));
                Set(controller, "actionBarView", root.AddComponent<DecorationActionBarView>());
                Set(controller, "storeModalView", root.AddComponent<DecorationStoreModalView>());
                controller.ConfigurePhase7Catalogue(catalogue, new[]
                {
                    new DecorationCategoryModel(
                        "furniture", "Furniture", Array.Empty<DecorationCatalogueItemModel>()),
                    new DecorationCategoryModel(
                        "cash-register", "Cash Register", new[]
                        {
                            new DecorationCatalogueItemModel(
                                RegisterDefinitionId, "Cash Register", null,
                                DecorationCatalogueItemKind.CashRegister, false, registerAsset)
                        }),
                    new DecorationCategoryModel(
                        "coffee-machine", "Coffee Machine", new[]
                        {
                            new DecorationCatalogueItemModel(
                                CoffeeMachineDefinitionId, "Coffee Machine", null,
                                DecorationCatalogueItemKind.CoffeeMachine, false, coffeeAsset)
                        })
                });
                Invoke(controller, "SubscribeViewEvents");
                catalogue.ShowCatalogue();
                catalogue.SetSheetState(DecorationSheetState.Expanded, hasActivePreview: false);

                Assert.That(catalogue.CategoryRows, Has.Count.EqualTo(3),
                    "The Furniture tab keeps Furniture, Cash Register and Coffee Machine rows.");
                var tiles = root.GetComponentsInChildren<DecorationCatalogueTileView>(true);
                tiles.Single(tile => tile.ItemId == RegisterDefinitionId)
                    .GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                Assert.That(controller.ActiveFunctionalSurfacePreview.DefinitionId,
                    Is.EqualTo(RegisterDefinitionId));
                Invoke(controller, "HandleCancelRequested");
                Assert.That(catalogue.SheetState, Is.EqualTo(DecorationSheetState.Expanded));

                tiles.Single(tile => tile.ItemId == CoffeeMachineDefinitionId)
                    .GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                Assert.That(controller.ActiveFunctionalSurfacePreview.DefinitionId,
                    Is.EqualTo(CoffeeMachineDefinitionId));
                Invoke(controller, "HandleCancelRequested");
                Assert.That(catalogue.SheetState, Is.EqualTo(DecorationSheetState.Expanded));

                pickUpButton.onClick.Invoke();
                Assert.That(controller.ActiveFunctionalSurfacePreview.Kind,
                    Is.EqualTo(FunctionalSurfacePreviewKind.PickUpPoint));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(registerAsset);
                UnityEngine.Object.DestroyImmediate(coffeeAsset);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ValidationMessageView_RetainsDetachedDiagnosticIdsAndClearsOnValidInput()
        {
            var root = new GameObject("Phase8ValidationMessage", typeof(RectTransform));
            var labelObject = new GameObject("Label", typeof(RectTransform));
            try
            {
                labelObject.transform.SetParent(root.transform, false);
                var label = labelObject.AddComponent<TextMeshProUGUI>();
                var view = root.AddComponent<ValidationMessageView>();
                view.Configure(label);
                var ids = new List<string> { "support.1", "slot.0", "support.1" };

                view.SetValidationResult(false, "这个摆放位已经被占用", ids);
                ids.Clear();

                Assert.That(view.IsVisible, Is.True);
                Assert.That(view.DiagnosticIds, Is.EqualTo(new[] { "support.1", "slot.0" }));
                Assert.That(view.CurrentMessage, Does.Not.Contain("support.1"));
                Assert.That(view.CurrentMessage, Does.Not.Contain("slot.0"));
                Assert.That(label.text, Does.Not.Contain("support.1"));
                Assert.That(label.text, Does.Not.Contain("slot.0"));
                view.SetValidationResult(true, null);
                Assert.That(view.IsVisible, Is.False);
                Assert.That(view.DiagnosticIds, Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(labelObject);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Controller_RepeatedSubscriptionKeepsOneListenerPerViewEvent()
        {
            var root = new GameObject("Phase8IdempotentSubscriptions");
            try
            {
                var controller = root.AddComponent<DecorationModeController>();
                var catalogue = root.AddComponent<DecorationCatalogueView>();
                var actionBar = root.AddComponent<DecorationActionBarView>();
                var storeModal = root.AddComponent<DecorationStoreModalView>();
                Set(controller, "catalogueView", catalogue);
                Set(controller, "actionBarView", actionBar);
                Set(controller, "storeModalView", storeModal);

                Invoke(controller, "SubscribeViewEvents");
                Invoke(controller, "SubscribeViewEvents");

                AssertListenerCount(catalogue, "PickUpPointRequested", 1);
                AssertListenerCount(catalogue, "Selected", 1);
                AssertListenerCount(actionBar, "ConfirmRequested", 1);
                AssertListenerCount(storeModal, "ConfirmRequested", 1);

                Invoke(controller, "UnsubscribeViewEvents");
                AssertListenerCount(catalogue, "PickUpPointRequested", 0);
                AssertListenerCount(actionBar, "ConfirmRequested", 0);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static FunctionalSurfaceLayout CreateFunctionalLayout()
        {
            return CreateScenario().Functional;
        }

        private const string ReadinessSlotId = "slot.center";
        private const string ReadinessCashSupportId = "12121212121212121212121212121212";
        private const string ReadinessCoffeeSupportId = "23232323232323232323232323232323";
        private const string ReadinessPickUpSupportId = "34343434343434343434343434343434";
        private const string ReadinessExtraSupportId = "45454545454545454545454545454545";
        private const string ReadinessCoffeeInstanceId = "abababababababababababababababab";
        private const string ReadinessPickUpInstanceId = "bcbcbcbcbcbcbcbcbcbcbcbcbcbcbcbc";
        private const string ReadinessExtraCashInstanceId = "cdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcd";

        private static Scenario CreateReadinessScenario(
            ReadinessPresentationCase presentationCase)
        {
            var catalog = new FurnitureDefinitionCatalog(new[]
            {
                new FurnitureDefinition(
                    SupportDefinitionId,
                    "One-cell Counter",
                    new GridSize(1, 1),
                    PlacementSurfaceType.Floor),
                new FurnitureDefinition(
                    RegisterDefinitionId,
                    "Cash Register",
                    new GridSize(1, 1),
                    PlacementSurfaceType.FurnitureSurface,
                    FurnitureFunctionType.CashRegister),
                new FurnitureDefinition(
                    CoffeeMachineDefinitionId,
                    "Coffee Machine",
                    new GridSize(1, 1),
                    PlacementSurfaceType.FurnitureSurface,
                    FurnitureFunctionType.CoffeeMachine)
            });
            var cafe = new CafeLayout(new GridSettings(1f), catalog);
            var slots = new SurfaceSlotCatalog(new[]
            {
                new SurfaceSlotDefinition(
                    SupportDefinitionId,
                    ReadinessSlotId,
                    new GridPosition(0, 0))
            });
            var directions = new FunctionalDirectionCatalog(
                new Dictionary<string, CashRegisterSides>
                {
                    [RegisterDefinitionId] = new CashRegisterSides(
                        CardinalDirection.North,
                        CardinalDirection.South)
                },
                new Dictionary<string, CardinalDirection>
                {
                    [CoffeeMachineDefinitionId] = CardinalDirection.North
                });
            var scenario = new Scenario(
                cafe,
                new FunctionalSurfaceLayout(cafe, catalog, slots),
                slots,
                directions);

            if (presentationCase == ReadinessPresentationCase.DisconnectedComplete)
            {
                AddReadinessRegion(scenario, "region.cash", 0, 0, 5, 5);
                AddReadinessRegion(scenario, "region.coffee", 10, 0, 5, 5);
                AddReadinessRegion(scenario, "region.pickup", 20, 0, 5, 5);
                AddReadinessEntrance(scenario, "entrance.cash", 0, 0);
                AddReadinessEntrance(scenario, "entrance.coffee", 10, 0);
                AddReadinessEntrance(scenario, "entrance.pickup", 20, 0);
                PlaceReadinessSupport(scenario, ReadinessCashSupportId, 2, 2);
                PlaceReadinessSupport(scenario, ReadinessCoffeeSupportId, 12, 2);
                PlaceReadinessSupport(scenario, ReadinessPickUpSupportId, 22, 2);
            }
            else
            {
                AddReadinessRegion(scenario, "region.main", 0, 0, 20, 12);
                AddReadinessEntrance(scenario, "entrance.main", 0, 0);
                PlaceReadinessSupport(scenario, ReadinessCashSupportId, 2, 5);
                PlaceReadinessSupport(scenario, ReadinessCoffeeSupportId, 6, 5);
                PlaceReadinessSupport(scenario, ReadinessPickUpSupportId, 10, 5);
            }

            if (presentationCase != ReadinessPresentationCase.MissingCoffee)
            {
                Assert.That(scenario.Functional.PlaceMounted(new SurfaceMountedInstance(
                    ReadinessCoffeeInstanceId,
                    CoffeeMachineDefinitionId,
                    ReadinessAddress(ReadinessCoffeeSupportId),
                    FurnitureRotation.Degrees0)).Succeeded, Is.True);
            }
            Assert.That(scenario.Functional.PlacePickUp(new PickUpPointInstance(
                ReadinessPickUpInstanceId,
                ReadinessAddress(ReadinessPickUpSupportId))).Succeeded, Is.True);

            if (presentationCase == ReadinessPresentationCase.ReadyWithInvalidExtra)
            {
                PlaceReadinessSupport(scenario, ReadinessExtraSupportId, 14, 5);
                Assert.That(scenario.Functional.PlaceMounted(new SurfaceMountedInstance(
                    ReadinessExtraCashInstanceId,
                    RegisterDefinitionId,
                    ReadinessAddress(ReadinessExtraSupportId),
                    FurnitureRotation.Degrees0)).Succeeded, Is.True);
                scenario.Cafe.AddReservation(new LayoutReservation(
                    "blocked.extra-register",
                    LayoutReservationType.Blocked,
                    new GridPosition(14, 6),
                    new GridSize(1, 1)));
            }
            return scenario;
        }

        private static void AssertReadinessMessageSurvivesPreviewAndCancel(
            DecorationModeController controller,
            CafeLayoutRuntime runtime,
            ValidationMessageView validation)
        {
            var published = runtime.CurrentReadiness;
            var version = runtime.ReadinessVersion;
            var message = validation.CurrentMessage;
            var wasVisible = validation.IsVisible;
            var ids = validation.DiagnosticIds.ToArray();
            Assert.That(controller.TryBeginExistingFunctionalSurfacePreview(
                FunctionalSurfacePreviewKind.MountedEquipment,
                ReadinessCoffeeInstanceId), Is.True);
            Assert.That(validation.CurrentMessage, Is.EqualTo(message));
            Assert.That(validation.IsVisible, Is.EqualTo(wasVisible));
            Assert.That(controller.TryMoveFunctionalSurfacePreview(
                ReadinessAddress(ReadinessCashSupportId)), Is.False);
            Assert.That(validation.CurrentMessage, Is.EqualTo(message));
            Assert.That(validation.DiagnosticIds, Is.EqualTo(ids));
            controller.CancelFunctionalSurfacePreview();
            Assert.That(validation.CurrentMessage, Is.EqualTo(message));
            Assert.That(validation.IsVisible, Is.EqualTo(wasVisible),
                "Cancel must not resurrect a healthy readiness banner.");
            Assert.That(validation.DiagnosticIds, Is.EqualTo(ids));
            Assert.That(runtime.CurrentReadiness, Is.SameAs(published));
            Assert.That(runtime.ReadinessVersion, Is.EqualTo(version));
        }

        private static void AddReadinessRegion(
            Scenario scenario,
            string id,
            int x,
            int y,
            int width,
            int height)
        {
            scenario.Cafe.AddRegion(new LayoutRegion(
                id,
                new GridPosition(x, y),
                new GridSize(width, height),
                LayoutZoneType.Interior));
        }

        private static void AddReadinessEntrance(
            Scenario scenario,
            string id,
            int x,
            int y)
        {
            scenario.Cafe.AddReservation(new LayoutReservation(
                id,
                LayoutReservationType.EntranceClearance,
                new GridPosition(x, y),
                new GridSize(1, 1)));
        }

        private static void PlaceReadinessSupport(
            Scenario scenario,
            string instanceId,
            int x,
            int y)
        {
            Assert.That(scenario.Cafe.PlaceFurniture(FurnitureInstance.Restore(
                instanceId,
                SupportDefinitionId,
                new GridPosition(x, y),
                FurnitureRotation.Degrees0)).Succeeded, Is.True);
        }

        private static SurfaceSlotAddress ReadinessAddress(string supportId)
        {
            return new SurfaceSlotAddress(supportId, ReadinessSlotId);
        }

        private static Scenario CreateScenario()
        {
            var catalog = new FurnitureDefinitionCatalog(new[]
            {
                new FurnitureDefinition(
                    SupportDefinitionId,
                    "Long Counter",
                    new GridSize(5, 1),
                    PlacementSurfaceType.Floor),
                new FurnitureDefinition(
                    RegisterDefinitionId,
                    "Cash Register",
                    new GridSize(1, 1),
                    PlacementSurfaceType.FurnitureSurface,
                    FurnitureFunctionType.CashRegister),
                new FurnitureDefinition(
                    CoffeeMachineDefinitionId,
                    "Coffee Machine",
                    new GridSize(1, 1),
                    PlacementSurfaceType.FurnitureSurface,
                    FurnitureFunctionType.CoffeeMachine)
            });
            var cafeLayout = new CafeLayout(new GridSettings(1f), catalog);
            cafeLayout.AddRegion(new LayoutRegion(
                "region.main",
                new GridPosition(0, 0),
                new GridSize(8, 8),
                LayoutZoneType.Interior));
            Assert.That(cafeLayout.PlaceFurniture(FurnitureInstance.Restore(
                SupportInstanceId,
                SupportDefinitionId,
                new GridPosition(0, 0),
                FurnitureRotation.Degrees0)).Succeeded, Is.True);
            cafeLayout.AddReservation(new LayoutReservation(
                "entrance.main",
                LayoutReservationType.EntranceClearance,
                new GridPosition(7, 7),
                new GridSize(1, 1)));
            var slots = new SurfaceSlotCatalog(new[]
            {
                new SurfaceSlotDefinition(
                    SupportDefinitionId,
                    "slot.0",
                    new GridPosition(0, 0)),
                new SurfaceSlotDefinition(
                    SupportDefinitionId,
                    "slot.1",
                    new GridPosition(1, 0)),
                new SurfaceSlotDefinition(
                    SupportDefinitionId,
                    "slot.2",
                    new GridPosition(2, 0)),
                new SurfaceSlotDefinition(
                    SupportDefinitionId,
                    "slot.3",
                    new GridPosition(3, 0)),
                new SurfaceSlotDefinition(
                    SupportDefinitionId,
                    "slot.4",
                    new GridPosition(4, 0))
            });
            var functional = new FunctionalSurfaceLayout(
                cafeLayout,
                catalog,
                slots);
            return new Scenario(cafeLayout, functional, slots);
        }

        private static void ConfigureRuntime(CafeLayoutRuntime runtime, Scenario scenario)
        {
            var directions = scenario.Directions ?? new FunctionalDirectionCatalog(
                new Dictionary<string, CashRegisterSides>
                {
                    [RegisterDefinitionId] = new CashRegisterSides(
                        CardinalDirection.North,
                        CardinalDirection.South)
                },
                new Dictionary<string, CardinalDirection>
                {
                    [CoffeeMachineDefinitionId] = CardinalDirection.South
                });
            var report = new LayoutReadinessEvaluator().Evaluate(
                scenario.Cafe,
                scenario.Functional,
                scenario.Slots,
                directions);
            Set(runtime, "<Layout>k__BackingField", scenario.Cafe);
            Set(runtime, "<FunctionalSurfaceLayout>k__BackingField", scenario.Functional);
            Set(runtime, "surfaceSlotCatalog", scenario.Slots);
            Set(runtime, "functionalDirectionCatalog", directions);
            Set(runtime, "currentReadiness", report);
            Set(runtime, "<ReadinessVersion>k__BackingField", 1);
        }

        private static FurnitureDefinitionAsset CreateDefinitionAsset(
            string definitionId,
            FurnitureFunctionType functionType)
        {
            var definition = ScriptableObject.CreateInstance<FurnitureDefinitionAsset>();
            Set(definition, "definitionId", definitionId);
            Set(definition, "displayName", definitionId);
            Set(definition, "allowedPlacementSurfaces", PlacementSurfaceType.FurnitureSurface);
            Set(definition, "functionType", functionType);
            return definition;
        }

        private static SurfaceSlotAddress Address(string slotId)
        {
            return new SurfaceSlotAddress(SupportInstanceId, slotId);
        }

        private static DecorationTouchFrame Frame(
            int frameNumber,
            params DecorationTouchPoint[] points)
        {
            return new DecorationTouchFrame(frameNumber, points);
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

        private sealed class ControlledTouchSource : IDecorationTouchSource
        {
            private int frameNumber;
            private DecorationTouchPoint[] points = Array.Empty<DecorationTouchPoint>();
            public void SetFrame(int number, params DecorationTouchPoint[] touches)
            {
                frameNumber = number;
                points = touches;
            }
            public DecorationTouchFrame ReadFrame() => new DecorationTouchFrame(frameNumber, points);
        }

        private sealed class FixedClassifier : IDecorationTouchHitClassifier
        {
            private readonly DecorationTouchHit hit;
            public FixedClassifier(DecorationTouchHit hit) => this.hit = hit;
            public DecorationTouchHit ClassifyBegan(int touchId, Vector2 screenPosition) => hit;
            public DecorationTouchHit ClassifyCurrent(int touchId, Vector2 screenPosition) => hit;
        }

        private sealed class SequencedClassifier : IDecorationTouchHitClassifier
        {
            private readonly DecorationTouchHit began;
            private readonly DecorationTouchHit current;
            public SequencedClassifier(DecorationTouchHit began, DecorationTouchHit current)
            {
                this.began = began;
                this.current = current;
            }
            public int CurrentCalls { get; private set; }
            public DecorationTouchHit ClassifyBegan(int touchId, Vector2 screenPosition) => began;
            public DecorationTouchHit ClassifyCurrent(int touchId, Vector2 screenPosition)
            {
                CurrentCalls++;
                return current;
            }
        }

        private static void Set(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }

        private static object Invoke(object target, string methodName, params object[] arguments)
        {
            var method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);
            return method.Invoke(target, arguments);
        }

        private static void AssertProjectedViews(
            GameObject mountedA,
            GameObject mountedB,
            GameObject pickUpA,
            GameObject pickUpB,
            Vector3 expectedMountedA,
            Vector3 expectedMountedB,
            Vector3 expectedPickUpA,
            Vector3 expectedPickUpB)
        {
            Assert.That(mountedA.activeSelf, Is.True);
            Assert.That(mountedB.activeSelf, Is.True);
            Assert.That(pickUpA.activeSelf, Is.True);
            Assert.That(pickUpB.activeSelf, Is.True);
            AssertWorldPosition(mountedA, expectedMountedA);
            AssertWorldPosition(mountedB, expectedMountedB);
            AssertWorldPosition(pickUpA, expectedPickUpA);
            AssertWorldPosition(pickUpB, expectedPickUpB);
        }

        private static void AssertWorldPosition(GameObject target, Vector3 expected)
        {
            var actual = target.transform.position;
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.0001f), target.name);
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.0001f), target.name);
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(0.0001f), target.name);
        }

        private static void AssertListenerCount(
            object source,
            string eventName,
            int expected)
        {
            var field = source.GetType().GetField(
                eventName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, eventName);
            var callback = field.GetValue(source) as Delegate;
            Assert.That(callback?.GetInvocationList().Length ?? 0, Is.EqualTo(expected));
        }

        private static int ActiveDirectChildCount(Transform root)
        {
            return Enumerable.Range(0, root.childCount)
                .Count(index => root.GetChild(index).gameObject.activeSelf);
        }

        private sealed class FunctionalViewHarness : IDisposable
        {
            private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
            private readonly FurnitureContentCatalog contentCatalog;

            public FunctionalViewHarness()
            {
                Scenario = CreateScenario();
                var material = Own(new Material(
                    Shader.Find("Universal Render Pipeline/Unlit")
                    ?? Shader.Find("Universal Render Pipeline/Lit")
                    ?? Shader.Find("Sprites/Default")));
                var theme = Own(ScriptableObject.CreateInstance<AnimalCafeUiTheme>());
                theme.Colors = new UiSemanticColorTokens
                {
                    Accent = new Color(0.18f, 0.82f, 0.38f, 0.95f),
                    Destructive = new Color(0.92f, 0.20f, 0.22f, 0.95f)
                };

                var supportPrefab = Own(CreateSupportPrefab(material));
                var registerPrefab = Own(CreateVisualPrefab("RegisterPrefab", material));
                var coffeePrefab = Own(CreateVisualPrefab("CoffeePrefab", material));
                var footprintPrefab = Own(CreateVisualPrefab("FootprintPrefab", material));
                var pickUpPrefab = Own(CreatePickUpVisualPrefab(material));
                var supportAsset = Own(CreateDefinitionAsset(
                    SupportDefinitionId,
                    FurnitureFunctionType.None));
                SupportAsset = supportAsset;
                Set(supportAsset, "allowedPlacementSurfaces", PlacementSurfaceType.Floor);
                Set(supportAsset, "footprintWidth", 5);
                Set(supportAsset, "footprintDepth", 1);
                Set(supportAsset, "prefab", supportPrefab);
                var registerAsset = Own(CreateDefinitionAsset(
                    RegisterDefinitionId,
                    FurnitureFunctionType.CashRegister));
                Set(registerAsset, "prefab", registerPrefab);
                var coffeeAsset = Own(CreateDefinitionAsset(
                    CoffeeMachineDefinitionId,
                    FurnitureFunctionType.CoffeeMachine));
                Set(coffeeAsset, "prefab", coffeePrefab);
                contentCatalog = Own(ScriptableObject.CreateInstance<FurnitureContentCatalog>());
                Set(contentCatalog, "entries", new List<FurnitureDefinitionAsset>
                {
                    supportAsset,
                    registerAsset,
                    coffeeAsset
                });
                contentCatalog.BuildRuntimeCatalog();

                Root = Own(new GameObject("Phase8FunctionalViewHarness"));
                Runtime = Root.AddComponent<CafeLayoutRuntime>();
                ConfigureRuntime(Runtime, Scenario);
                var gridSpace = new DecorationGridSpace(
                    Scenario.Cafe.GridSettings,
                    new LayoutBounds(new GridPosition(0, 0), new GridSize(8, 8)));
                var furnitureRoot = CreateChild("FurnitureRoot");
                FurnitureRegistry = Root.AddComponent<FurnitureSceneRegistry>();
                FurnitureRegistry.Configure(contentCatalog, furnitureRoot.transform, gridSpace);
                FurnitureRegistry.Rebuild(Scenario.Cafe.FurnitureInstances);
                FurniturePreviewRoot = CreateChild("FurniturePreviewRoot");
                FurniturePreview = Root.AddComponent<FurniturePreviewView>();
                FurniturePreview.Configure(FurniturePreviewRoot.transform, gridSpace, theme);
                var gridVisualRoot = CreateChild("FurnitureGridVisualRoot");
                FurnitureGrid = Root.AddComponent<GridHighlightView>();
                FurnitureGrid.Configure(
                    gridVisualRoot.transform,
                    gridSpace,
                    material,
                    theme);
                OrdinarySession = new DecorationSession(Scenario.Cafe, Scenario.Functional);
                OrdinarySession.Enter();

                MountedRoot = CreateChild("MountedRoot");
                MountedRegistry = Root.AddComponent<SurfaceMountedSceneRegistry>();
                MountedRegistry.Configure(
                    contentCatalog,
                    FurnitureRegistry,
                    MountedRoot.transform);
                PreviewRoot = CreateChild("PreviewRoot");
                MountedPreview = Root.AddComponent<SurfaceMountedPreviewView>();
                MountedPreview.Configure(
                    contentCatalog,
                    FurnitureRegistry,
                    PreviewRoot.transform,
                    footprintPrefab,
                    theme);
                PickUpRoot = CreateChild("PickUpRoot");
                PickUpIndicators = Root.AddComponent<PickUpPointIndicatorView>();
                PickUpIndicators.Configure(
                    FurnitureRegistry,
                    PickUpRoot.transform,
                    pickUpPrefab,
                    theme);

                var cameraObject = CreateChild("Camera");
                Camera = cameraObject.AddComponent<UnityEngine.Camera>();
                cameraObject.transform.position = new Vector3(1.5f, 10f, 0.5f);
                cameraObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                Camera.orthographic = true;
                Camera.orthographicSize = 5f;

                var canvasObject = Own(new GameObject(
                    "ActionCanvas",
                    typeof(RectTransform),
                    typeof(Canvas)));
                canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                var actionBarObject = new GameObject("ActionBar", typeof(RectTransform));
                actionBarObject.transform.SetParent(canvasObject.transform, false);
                ActionBarRect = actionBarObject.GetComponent<RectTransform>();
                ActionBar = actionBarObject.AddComponent<DecorationActionBarView>();
                var catalogueObject = new GameObject("Catalogue", typeof(RectTransform));
                catalogueObject.transform.SetParent(Root.transform, false);
                Catalogue = catalogueObject.AddComponent<DecorationCatalogueView>();
                StoreModal = Root.AddComponent<DecorationStoreModalView>();
                CameraDriver = Root.AddComponent<DecorationCameraDriver>();

                Controller = Root.AddComponent<DecorationModeController>();
                Set(Controller, "layoutRuntime", Runtime);
                Set(Controller, "contentCatalog", contentCatalog);
                Session = new FunctionalSurfaceDecorationSession(Scenario.Functional);
                Set(Controller, "functionalSurfaceSession", Session);
                Set(Controller, "session", OrdinarySession);
                Set(Controller, "sceneRegistry", FurnitureRegistry);
                Set(Controller, "previewView", FurniturePreview);
                Set(Controller, "gridView", FurnitureGrid);
                Set(Controller, "cameraDriver", CameraDriver);
                Set(Controller, "catalogueView", Catalogue);
                Set(Controller, "storeModalView", StoreModal);
                Set(Controller, "gridSpace", gridSpace);
                Set(Controller, "gridRoot", Root.transform);
                Set(Controller, "targetCamera", Camera);
                Set(Controller, "actionBarView", ActionBar);
                Router = new DecorationTouchRouter(8f, 0f);
                Set(Controller, "touchRouter", Router);
                Set(Controller, "isOpen", true);
                Controller.ConfigurePhase8Scene(
                    MountedRegistry,
                    MountedPreview,
                    PickUpIndicators);
            }

            public GameObject Root { get; }
            public FurnitureDefinitionAsset SupportAsset { get; }
            public Button StoreConfirm { get; private set; }
            public Button StoreCancel { get; private set; }
            public Scenario Scenario { get; }
            public CafeLayoutRuntime Runtime { get; }
            public DecorationSession OrdinarySession { get; }
            public FurniturePreviewView FurniturePreview { get; }
            public GridHighlightView FurnitureGrid { get; }
            public DecorationCameraDriver CameraDriver { get; }
            public DecorationCatalogueView Catalogue { get; }
            public DecorationStoreModalView StoreModal { get; }
            public FurnitureSceneRegistry FurnitureRegistry { get; }
            public SurfaceMountedSceneRegistry MountedRegistry { get; }
            public SurfaceMountedPreviewView MountedPreview { get; }
            public PickUpPointIndicatorView PickUpIndicators { get; }
            public DecorationModeController Controller { get; }
            public FunctionalSurfaceDecorationSession Session { get; }
            public DecorationTouchRouter Router { get; }
            public DecorationActionBarView ActionBar { get; }
            public RectTransform ActionBarRect { get; }
            public UnityEngine.Camera Camera { get; }
            public GameObject MountedRoot { get; }
            public GameObject PreviewRoot { get; }
            public GameObject PickUpRoot { get; }
            public GameObject FurniturePreviewRoot { get; }

            public bool SelectMountedCatalogueItem(DecorationCatalogueItemKind kind, string definitionId)
            {
                Assert.That(contentCatalog.TryGetDefinitionAsset(definitionId, out var asset), Is.True);
                return Controller.TrySelectCatalogueItem(new DecorationCatalogueItemModel(
                    definitionId, definitionId, null, kind, false, asset));
            }

            public void BeginMountedPreview(
                DecorationCatalogueItemKind kind, string definitionId, bool existing)
            {
                const string instanceId = "56565656565656565656565656565656";
                if (existing)
                {
                    Assert.That(Scenario.Functional.PlaceMounted(new SurfaceMountedInstance(
                        instanceId, definitionId, Address("slot.0"), FurnitureRotation.Degrees0))
                        .Succeeded, Is.True);
                    RebuildConfirmedViews();
                    Assert.That(Controller.TryBeginExistingFunctionalSurfacePreview(
                        FunctionalSurfacePreviewKind.MountedEquipment, instanceId), Is.True);
                    Assert.That(MountedRegistry.TryGet(instanceId, out var source), Is.True);
                    Assert.That(source.activeSelf, Is.False);
                }
                else
                {
                    Assert.That(Controller.TryBeginFunctionalSurfacePreview(
                        kind, definitionId, Address("slot.0")), Is.True);
                }
            }

            public void BeginPickUpPreview(bool existing)
            {
                const string instanceId = "56565656565656565656565656565656";
                if (existing)
                {
                    Assert.That(Scenario.Functional.PlacePickUp(new PickUpPointInstance(
                        instanceId, Address("slot.0"))).Succeeded, Is.True);
                    RebuildConfirmedViews();
                    Assert.That(Controller.TryBeginExistingFunctionalSurfacePreview(
                        FunctionalSurfacePreviewKind.PickUpPoint, instanceId), Is.True);
                    Assert.That(PickUpIndicators.TryGet(instanceId, out var source), Is.True);
                    Assert.That(source.activeSelf, Is.False);
                }
                else
                {
                    Assert.That(Controller.TryBeginFunctionalSurfacePreview(
                        DecorationCatalogueItemKind.PickUpPoint, null, Address("slot.0")), Is.True);
                }
            }

            public DecorationTouchRoutingResult RouteMountedPointer(
                Vector3 worldPoint, InputTouchPhase phase, int sequence)
            {
                var screen = Camera.WorldToScreenPoint(worldPoint);
                var result = Router.ProcessFrame(
                    Frame(sequence, Point(71, screen.x, screen.y, phase)),
                    (IDecorationTouchHitClassifier)Controller);
                Controller.RouteTouchResultForActiveMode(result);
                return result;
            }

            public void ConfigureStoreModal()
            {
                var modalRoot = new GameObject("FunctionalStoreModal", typeof(RectTransform),
                    typeof(CanvasGroup));
                modalRoot.transform.SetParent(Root.transform, false);
                var modal = modalRoot.AddComponent<AnimalCafeModalView>();
                var blocker = CreateModalButton("Blocker", modalRoot.transform);
                StoreConfirm = CreateModalButton("Confirm", modalRoot.transform);
                StoreCancel = CreateModalButton("Cancel", modalRoot.transform);
                Set(StoreModal, "modalView", modal);
                Set(StoreModal, "confirmButton", StoreConfirm);
                Set(StoreModal, "cancelButton", StoreCancel);
                Set(StoreModal, "modalBlocker", blocker);
                Set(StoreModal, "canvasGroup", modalRoot.GetComponent<CanvasGroup>());
                StoreModal.Configure(new UiNavigationCoordinator(),
                    new UiPauseCoordinator(Root.AddComponent<GameTimeService>()),
                    new UiPointerBoundary(), new UiTransitionRunner(() => true));
                Invoke(Controller, "SubscribeViewEvents");
            }

            private static Button CreateModalButton(string name, Transform parent)
            {
                var button = new GameObject(name, typeof(RectTransform), typeof(Button));
                button.transform.SetParent(parent, false);
                return button.GetComponent<Button>();
            }

            public void RebuildConfirmedViews()
            {
                MountedRegistry.Rebuild(Scenario.Functional.MountedInstances);
                PickUpIndicators.Rebuild(Scenario.Functional.PickUpPoints, true);
            }

            public void RebuildAllConfirmedViews()
            {
                FurnitureRegistry.Rebuild(Scenario.Cafe.FurnitureInstances);
                RebuildConfirmedViews();
            }

            public void AssertActionBarTracksCurrentPreview()
            {
                var target = MountedPreview.CurrentGhost != null
                    ? MountedPreview.CurrentGhost
                    : PickUpIndicators.CurrentPreview;
                Assert.That(target, Is.Not.Null);
                var renderers = target.GetComponentsInChildren<Renderer>(false);
                Assert.That(renderers, Is.Not.Empty);
                var bounds = renderers[0].bounds;
                for (var index = 1; index < renderers.Length; index++)
                {
                    bounds.Encapsulate(renderers[index].bounds);
                }

                var preferred = new Vector2(float.MinValue, float.MinValue);
                for (var index = 0; index < 8; index++)
                {
                    var corner = new Vector3(
                        (index & 1) == 0 ? bounds.min.x : bounds.max.x,
                        (index & 2) == 0 ? bounds.min.y : bounds.max.y,
                        (index & 4) == 0 ? bounds.min.z : bounds.max.z);
                    var screen = Camera.WorldToScreenPoint(corner);
                    preferred.x = Mathf.Max(preferred.x, screen.x);
                    preferred.y = Mathf.Max(preferred.y, screen.y);
                }
                preferred += new Vector2(8f, 8f);
                var actual = RectTransformUtility.WorldToScreenPoint(null, ActionBarRect.position);
                Assert.That(Vector2.Distance(actual, preferred), Is.LessThan(2f),
                    "The real ActionBar pivot must follow the current real preview bounds.");
            }

            public void Dispose()
            {
                for (var index = owned.Count - 1; index >= 0; index--)
                {
                    if (owned[index] != null)
                    {
                        UnityEngine.Object.DestroyImmediate(owned[index]);
                    }
                }
            }

            private GameObject CreateChild(string name)
            {
                var child = new GameObject(name);
                child.transform.SetParent(Root.transform, false);
                return child;
            }

            private T Own<T>(T item) where T : UnityEngine.Object
            {
                owned.Add(item);
                return item;
            }

            private static GameObject CreateSupportPrefab(Material material)
            {
                var root = CreateVisualPrefab("SupportPrefab", material);
                for (var index = 0; index < 5; index++)
                {
                    var markerObject = new GameObject("SlotMarker" + index);
                    markerObject.transform.SetParent(root.transform, false);
                    markerObject.transform.localPosition = new Vector3(index - 2f, 0.72f, 0f);
                    var marker = markerObject.AddComponent<SurfaceSlotMarker>();
                    Set(marker, "slotId", "slot." + index);
                }
                return root;
            }

            private static GameObject CreatePickUpVisualPrefab(Material material)
            {
                var root = CreateVisualPrefab("PickUpPrefab", material);
                root.transform.Find("Visual").name = "InvertedSquarePyramid";
                var footprint = GameObject.CreatePrimitive(PrimitiveType.Cube);
                footprint.name = "Footprint";
                footprint.transform.SetParent(root.transform, false);
                footprint.transform.localPosition = new Vector3(0f, 0.02f, 0f);
                footprint.transform.localScale = new Vector3(1f, 0.02f, 1f);
                footprint.GetComponent<Renderer>().sharedMaterial = material;
                return root;
            }

            private static GameObject CreateVisualPrefab(string name, Material material)
            {
                var root = new GameObject(name);
                var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                visual.name = "Visual";
                visual.transform.SetParent(root.transform, false);
                visual.transform.localPosition = new Vector3(0.2f, 0.3f, 0f);
                visual.transform.localScale = new Vector3(0.65f, 0.6f, 0.45f);
                visual.GetComponent<Renderer>().sharedMaterial = material;
                root.SetActive(false);
                return root;
            }
        }

        private sealed class Scenario
        {
            public Scenario(
                CafeLayout cafe,
                FunctionalSurfaceLayout functional,
                SurfaceSlotCatalog slots,
                FunctionalDirectionCatalog directions = null)
            {
                Cafe = cafe;
                Functional = functional;
                Slots = slots;
                Directions = directions;
            }

            public CafeLayout Cafe { get; }
            public FunctionalSurfaceLayout Functional { get; }
            public SurfaceSlotCatalog Slots { get; }
            public FunctionalDirectionCatalog Directions { get; }
        }

        public enum ReadinessPresentationCase
        {
            MissingCoffee,
            Complete,
            ReadyWithInvalidExtra,
            DisconnectedComplete
        }

        private enum ControllerCleanup
        {
            Exit,
            Disable,
            Destroy
        }
    }
}

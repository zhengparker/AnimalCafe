#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AnimalCafe.Decoration;
using AnimalCafe.Decoration.Input;
using AnimalCafe.Layout;
using AnimalCafe.UI.Components;
using AnimalCafe.UI.Decoration;
using AnimalCafe.UI.Feedback;
using AnimalCafe.UI.P8R;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace AnimalCafe.Tests.PlayMode.EditorSceneLoading
{
    /// <summary>
    /// Role indicators stay on the CR's real interaction cells, including blocked cells.
    /// 角色提示锚定真实站位；受阻只改变标志，不移动箭头或修改已确认布局。
    /// </summary>
    public sealed class P8RCashRegisterSideIndicatorTests
    {
        private Scene ownedScene;
        private Vector2? previousProfile;
        private float previousTimeScale;

        [SetUp]
        public void RecordBoundary()
        {
            previousProfile = P8RMobileMetrics.EditorLogicalViewportOverride;
            previousTimeScale = Time.timeScale;
        }

        [UnityTearDown]
        public IEnumerator ReleaseScene()
        {
            yield return UnloadScene();
            P8RMobileMetrics.EditorLogicalViewportOverride = previousProfile;
            Time.timeScale = previousTimeScale;
        }

        [UnityTest]
        public IEnumerator CashIndicator_CloseWideView_IconsClearCatalogueWithoutMovingGroundAnchors()
        {
            using (var screen = new P8RReferenceLayoutTests.NativeScreenSize())
            {
                screen.Resize(new Vector2(1600, 720));
                yield return LoadScene(new Vector2(800, 360));
                Component<DecorationModeController>().EnterDecorationMode();
                Select("equipment.cash-register.01");
                yield return Settle();
                UnityEngine.Camera.main.orthographicSize = 4;
                yield return Settle();
                AssertIndicatorVisible();
                var cards = Component<DecorationCatalogueView>().GetComponentsInChildren<Button>(true)
                    .Where(item => item.gameObject.activeInHierarchy && item.image != null).ToArray();
                // World anchors must not slide to escape UI. The floating explanation stays readable.
                // 地面锚点不为躲 UI 移位；可读性由上方角色图示与操作栏避让保证。
                AssertArrowCell("Employee", 2, 4);
                AssertArrowCell("Customer", 2, 2);
                foreach (var role in new[] { "Employee", "Customer" })
                foreach (var card in cards)
                    Assert.That(Box(Child(role + "Badge").Find("InvalidOverlay")).Overlaps(Box(card.image)), Is.False,
                        role + " icon and full prohibition ring must clear catalogue card " + card.name);
            }
        }

        [UnityTest]
        public IEnumerator CashIndicator_ArrowsStayOnAdjacentCells_WhenBothSidesAreBlocked()
        {
            yield return LoadScene(new Vector2(800, 600));
            var controller = Component<DecorationModeController>();
            var runtime = Component<CafeLayoutRuntime>();
            controller.EnterDecorationMode();
            foreach (var cell in new[] { new GridPosition(2, 4), new GridPosition(2, 2) })
                Assert.That(runtime.Layout.PlaceFurniture(FurnitureInstance.CreateNew(
                    "furniture.counter.module.01", cell, FurnitureRotation.Degrees0)).Succeeded, Is.True);
            Component<FurnitureSceneRegistry>().Rebuild(runtime.Layout.FurnitureInstances);
            Select("equipment.cash-register.01");
            var support = runtime.Layout.FurnitureInstances.Single(item => item.Position == new GridPosition(2, 3));
            Assert.That(controller.TryMoveFunctionalSurfacePreview(new SurfaceSlotAddress(support.InstanceId, "slot.0")), Is.True);
            yield return Settle();
            AssertArrowCell("Employee", 2, 4);
            AssertArrowCell("Customer", 2, 2);
            var employeeCells = new[] { new Vector2Int(2, 4), new Vector2Int(3, 3), new Vector2Int(2, 2), new Vector2Int(1, 3) };
            var customerCells = new[] { new Vector2Int(2, 2), new Vector2Int(1, 3), new Vector2Int(2, 4), new Vector2Int(3, 3) };
            for (var turn = 0; turn < 4; turn++)
            {
                AssertArrowCell("Employee", employeeCells[turn].x, employeeCells[turn].y);
                AssertArrowCell("Customer", customerCells[turn].x, customerCells[turn].y);
                AssertRoleIconsShareArrowRelativeHeight("fixed anchor turn " + turn);
                if (turn < 3) Assert.That(controller.TryRotateFunctionalSurfacePreview(), Is.True);
                yield return Settle();
            }
        }

        [UnityTest]
        public IEnumerator CashIndicator_BlockedRoleOnly_RecoversAfterRotateWithoutChangingConfirmedLayout()
        {
            yield return LoadScene(new Vector2(800, 600));
            var controller = Component<DecorationModeController>();
            var runtime = Component<CafeLayoutRuntime>();
            controller.EnterDecorationMode();
            Assert.That(runtime.Layout.PlaceFurniture(FurnitureInstance.CreateNew(
                "furniture.counter.module.01", new GridPosition(2, 4), FurnitureRotation.Degrees0)).Succeeded, Is.True);
            Component<FurnitureSceneRegistry>().Rebuild(runtime.Layout.FurnitureInstances);
            runtime.RecalculateReadiness();
            var report = runtime.CurrentReadiness;
            var version = runtime.ReadinessVersion;
            Select("equipment.cash-register.01");
            var support = runtime.Layout.FurnitureInstances.Single(item => item.Position == new GridPosition(2, 3));
            Assert.That(controller.TryMoveFunctionalSurfacePreview(new SurfaceSlotAddress(support.InstanceId, "slot.0")), Is.True);
            yield return Settle();
            AssertInvalidRole("Employee", true);
            AssertInvalidRole("Customer", false);
            Assert.That(controller.ActiveFunctionalSurfacePreview.CanConfirm, Is.True,
                "Blocked access is advisory; this change must not silently change slot placement rules.");
            Assert.That(controller.TryRotateFunctionalSurfacePreview(), Is.True);
            yield return Settle();
            AssertInvalidRole("Employee", false);
            AssertInvalidRole("Customer", false);
            Assert.That(controller.TryRotateFunctionalSurfacePreview(), Is.True);
            yield return Settle();
            AssertInvalidRole("Employee", false);
            AssertInvalidRole("Customer", true);
            Assert.That(runtime.CurrentReadiness, Is.SameAs(report));
            Assert.That(runtime.ReadinessVersion, Is.EqualTo(version));
            Assert.That(runtime.FunctionalSurfaceLayout.MountedInstances, Is.Empty);
            controller.CancelFunctionalSurfacePreview();
            yield return Settle();
            AssertIndicatorHidden("Cancel removes both invalid badges and fixed arrows");
        }

        [UnityTest]
        public IEnumerator CashIndicator_OccupiedSlot_DoesNotReportClearRoleCellsAsBlocked()
        {
            yield return LoadScene(new Vector2(800, 600));
            var controller = Component<DecorationModeController>();
            controller.EnterDecorationMode();
            Select("equipment.cash-register.01");
            var occupied = controller.ActiveFunctionalSurfacePreview.Address;
            Assert.That(controller.TryConfirmFunctionalSurfacePreview(), Is.True);
            var runtime = Component<CafeLayoutRuntime>();
            var report = runtime.CurrentReadiness;
            var version = runtime.ReadinessVersion;
            Select("equipment.cash-register.01");
            Assert.That(controller.TryMoveFunctionalSurfacePreview(occupied), Is.False);
            yield return Settle();
            Assert.That(controller.ActiveFunctionalSurfacePreview.CanConfirm, Is.False);
            AssertArrowCell("Employee", 2, 4);
            AssertArrowCell("Customer", 2, 2);
            AssertInvalidRole("Employee", false);
            AssertInvalidRole("Customer", false);
            Assert.That(runtime.FunctionalSurfaceLayout.MountedInstances.Count, Is.EqualTo(1));
            Assert.That(runtime.CurrentReadiness, Is.SameAs(report));
            Assert.That(runtime.ReadinessVersion, Is.EqualTo(version));
        }

        private void AssertArrowCell(string role, int x, int y)
        {
            var floor = Field<Transform>(Component<DecorationModeController>(), "gridRoot");
            var expected = floor.TransformPoint(new Vector3(x + .5f, .025f, y + .5f));
            var arrow = Components<MeshRenderer>().Single(item => item.name == role + "RoleArrow");
            Assert.That(Vector3.Distance(arrow.transform.position, expected), Is.LessThan(.002f),
                role + " must stay on its real adjacent cell even when furniture hides it.");
        }

        [UnityTest]
        public IEnumerator CashIndicator_RotatedLongCounter_UsesSlotCellInsteadOfWholeCounterEdge()
        {
            yield return LoadScene(new Vector2(800, 600));
            var runtime = Component<CafeLayoutRuntime>();
            var controller = Component<DecorationModeController>();
            controller.EnterDecorationMode();
            var support = FurnitureInstance.CreateNew("counter.preset.1x3", new GridPosition(4, 2), FurnitureRotation.Degrees90);
            Assert.That(runtime.Layout.PlaceFurniture(support).Succeeded, Is.True);
            Component<FurnitureSceneRegistry>().Rebuild(runtime.Layout.FurnitureInstances);
            Select("equipment.cash-register.01");
            Assert.That(controller.TryMoveFunctionalSurfacePreview(new SurfaceSlotAddress(support.InstanceId, "slot.1")), Is.True);
            // slot.1 rotates to (5,2). Local North becomes East; both neighbours are this support itself.
            var employees = new[] { new Vector2Int(6, 2), new Vector2Int(5, 1), new Vector2Int(4, 2), new Vector2Int(5, 3) };
            var customers = new[] { new Vector2Int(4, 2), new Vector2Int(5, 3), new Vector2Int(6, 2), new Vector2Int(5, 1) };
            for (var turn = 0; turn < 4; turn++)
            {
                yield return Settle();
                AssertArrowCell("Employee", employees[turn].x, employees[turn].y);
                AssertArrowCell("Customer", customers[turn].x, customers[turn].y);
                AssertInvalidRole("Employee", turn % 2 == 0);
                AssertInvalidRole("Customer", turn % 2 == 0);
                AssertRoleIconsShareArrowRelativeHeight("rotated long support " + turn);
                if (turn < 3) Assert.That(controller.TryRotateFunctionalSurfacePreview(), Is.True);
            }
        }

        [UnityTest]
        public IEnumerator CashIndicator_BlockedReservationAndOutOfBounds_OnlyMarkAffectedRole()
        {
            yield return LoadScene(new Vector2(800, 600));
            var runtime = Component<CafeLayoutRuntime>();
            var controller = Component<DecorationModeController>();
            controller.EnterDecorationMode();
            runtime.Layout.AddReservation(new LayoutReservation("cash.test.blocked", LayoutReservationType.Blocked,
                new GridPosition(2, 4), new GridSize(1, 1)));
            Select("equipment.cash-register.01");
            yield return Settle();
            AssertInvalidRole("Employee", true);
            AssertInvalidRole("Customer", false);
            AssertArrowCell("Employee", 2, 4);
            var edge = FurnitureInstance.CreateNew("furniture.counter.module.01", new GridPosition(2, 7), FurnitureRotation.Degrees0);
            Assert.That(runtime.Layout.PlaceFurniture(edge).Succeeded, Is.True);
            Component<FurnitureSceneRegistry>().Rebuild(runtime.Layout.FurnitureInstances);
            Assert.That(controller.TryMoveFunctionalSurfacePreview(new SurfaceSlotAddress(edge.InstanceId, "slot.0")), Is.True);
            yield return Settle();
            AssertArrowCell("Employee", 2, 8);
            AssertArrowCell("Customer", 2, 6);
            AssertInvalidRole("Employee", true);
            AssertInvalidRole("Customer", false);
            Assert.That(controller.ActiveFunctionalSurfacePreview.CanConfirm, Is.True);
            Assert.That(controller.TryMoveFunctionalSurfacePreview(default), Is.False);
            yield return Settle();
            AssertInvalidRole("Employee", true);
            AssertInvalidRole("Customer", true);
        }

        private void AssertInvalidRole(string role, bool expected)
        {
            var overlay = Child(role + "Badge").Find("InvalidOverlay");
            Assert.That(overlay, Is.Not.Null, "Each role needs its own prohibition overlay.");
            Assert.That(overlay.gameObject.activeSelf, Is.EqualTo(expected), role + " invalid state");
            Assert.That(overlay.GetComponent<Graphic>().raycastTarget, Is.False);
            if (expected)
            {
                AssertIndicatorVisible();
                var ring = Box(overlay);
                var icon = Box(Child(role + "Badge"));
                Assert.That(Vector2.Distance(ring.center, icon.center), Is.LessThan(.1f));
                Assert.That(ring.width, Is.GreaterThan(icon.width), "The ring surrounds rather than replaces the role icon.");
            }
        }

        [UnityTest]
        public IEnumerator CashIndicator_FourQuarterTurns_RoleIconsStayEqualHeightAboveTheirArrows()
        {
            using (var screen = new P8RReferenceLayoutTests.NativeScreenSize())
            {
                screen.Resize(new Vector2(1600, 720));
                yield return LoadScene(new Vector2(800, 360));
                var controller = Component<DecorationModeController>();
                controller.EnterDecorationMode();
                Select("equipment.cash-register.01");

                for (var turn = 0; turn < 4; turn++)
                {
                    yield return Settle();
                    AssertIndicatorVisible();
                    AssertRoleIconsShareArrowRelativeHeight("equipment turn " + turn);
                    AssertIconsAvoidActionFaces("equipment turn " + turn);
                    if (turn < 3)
                        Assert.That(controller.TryRotateFunctionalSurfacePreview(), Is.True);
                }
            }
        }

        [UnityTest]
        public IEnumerator CashIndicator_HoverHeight_MatchesConfirmedPickupAboveItsOwnGroundAnchor()
        {
            using (var screen = new P8RReferenceLayoutTests.NativeScreenSize())
            {
                screen.Resize(new Vector2(1600, 1200));
                yield return LoadScene(new Vector2(800, 600));
                var controller = Component<DecorationModeController>();
                var runtime = Component<CafeLayoutRuntime>();
                controller.EnterDecorationMode();
                var support = runtime.Layout.FurnitureInstances.Single(item => item.Position == new GridPosition(2, 3));
                Assert.That(controller.TryBeginFunctionalSurfacePreview(DecorationCatalogueItemKind.PickUpPoint,
                    null, new SurfaceSlotAddress(support.InstanceId, "slot.0")), Is.True);
                Assert.That(controller.TryConfirmFunctionalSurfacePreview(), Is.True);
                yield return Settle();
                Assert.That(Component<PickUpPointIndicatorView>().TryGet(runtime.FunctionalSurfaceLayout.PickUpPoints.Single().InstanceId,
                    out var pickup), Is.True);
                var sign = pickup.GetComponentInChildren<P8RPickUpSignBillboard>().GetComponent<Renderer>();
                Assert.That(pickup.activeInHierarchy && sign.enabled, Is.True, "Compare the visible confirmed Pickup sign.");
                Select("equipment.cash-register.01");
                var camera = UnityEngine.Camera.main;
                foreach (var zoom in new[] { 4f, 6f, 12f })
                {
                    camera.orthographicSize = zoom;
                    yield return Settle();
                    AssertIndicatorVisible();
                    // Measure real artwork, not the CR height formula / 独立比较常驻取餐牌的实际可见中心。
                    var ground = new Vector3(pickup.transform.position.x, .025f, pickup.transform.position.z);
                    var pickupHeight = camera.WorldToScreenPoint(sign.bounds.center).y - camera.WorldToScreenPoint(ground).y;
                    var pixels = P8RMobileMetrics.For(IndicatorRoot().GetComponent<CashRegisterSideIndicatorView>()).PixelsPerLogicalUnit;
                    foreach (var role in new[] { "Employee", "Customer" })
                    {
                        var arrow = Components<MeshRenderer>().Single(item => item.name == role + "RoleArrow");
                        var roleHeight = Box(Child(role + "Badge")).center.y - camera.WorldToScreenPoint(arrow.transform.position).y;
                        Assert.That(roleHeight, Is.EqualTo(pickupHeight).Within(4 * pixels),
                            role + " should hover near the confirmed Pickup sign height at zoom " + zoom);
                    }
                    AssertRoleIconsShareArrowRelativeHeight("Pickup reference zoom " + zoom);
                    AssertIconsAvoidActionFaces("Pickup reference zoom " + zoom);
                    AssertArrowCell("Employee", 2, 4);
                    AssertArrowCell("Customer", 2, 2);
                }
            }
        }

        private static Rect ProjectWorldBounds(Bounds bounds)
        {
            var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            for (var i = 0; i < 8; i++)
            {
                var point = bounds.center + Vector3.Scale(bounds.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                var screen = (Vector2)UnityEngine.Camera.main.WorldToScreenPoint(point);
                min = Vector2.Min(min, screen); max = Vector2.Max(max, screen);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        [UnityTest]
        public IEnumerator CashIndicator_BobbingIcons_DoNotMoveTheActionRow()
        {
            yield return LoadScene(new Vector2(800, 600));
            Component<DecorationModeController>().EnterDecorationMode();
            Select("equipment.cash-register.01");
            yield return Settle();
            var action = Action("rotateButton");
            var initialPosition = Box(action).center;
            var minimumIconY = float.PositiveInfinity;
            var maximumIconY = float.NegativeInfinity;
            for (var sample = 0; sample < 5; sample++)
            {
                yield return new WaitForSecondsRealtime(.5f);
                AssertIndicatorVisible();
                AssertRoleIconsShareArrowRelativeHeight("bob sample " + sample);
                AssertIconsAvoidActionFaces("bob sample " + sample);
                Assert.That(Vector2.Distance(Box(action).center, initialPosition), Is.LessThan(.1f),
                    "A stationary CR's action row must not chase the icons' bob animation");
                var y = Box(Child("EmployeeBadge")).center.y;
                minimumIconY = Mathf.Min(minimumIconY, y);
                maximumIconY = Mathf.Max(maximumIconY, y);
            }
            Assert.That(maximumIconY - minimumIconY, Is.GreaterThan(.5f),
                "The approved subtle icon animation should still be running");
        }

        [UnityTest]
        public IEnumerator CashIndicator_HeldActionTarget_DoesNotMoveUntilPointerRelease()
        {
            using (var screen = new P8RReferenceLayoutTests.NativeScreenSize())
            {
                screen.Resize(new Vector2(1600, 720));
                yield return LoadScene(new Vector2(800, 360));
                var controller = Component<DecorationModeController>();
                controller.EnterDecorationMode();
                Select("equipment.cash-register.01");
                yield return Settle();
                var action = Action("rotateButton");
                var initialPosition = Box(action).center;
                var pointer = new PointerEventData(EventSystem.current)
                    { pointerId = 712, position = initialPosition, button = PointerEventData.InputButton.Left };
                ExecuteEvents.Execute(action.gameObject, pointer, ExecuteEvents.pointerDownHandler);
                try
                {
                    Assert.That(action.GetComponent<DecorationPointerBoundaryEventHook>().HasActivePress, Is.True);
                    UnityEngine.Camera.main.orthographicSize = 4;
                    yield return Settle();
                    Assert.That(Vector2.Distance(Box(action).center, initialPosition), Is.LessThan(.1f),
                        "A held action target must remain under the pointer while the CR obstacle changes");
                }
                finally
                {
                    ExecuteEvents.Execute(action.gameObject, pointer, ExecuteEvents.pointerUpHandler);
                }
                yield return Settle();
                Assert.That(controller.ActiveFunctionalSurfacePreview, Is.Not.Null,
                    "A press/release without Click must not confirm or cancel the CR preview");
                AssertIndicatorVisible();
                AssertRoleIconsShareArrowRelativeHeight("after pointer release");
                AssertIconsAvoidActionFaces("after pointer release");
            }
        }

        [UnityTest]
        public IEnumerator CashIndicator_GroundArrowsAndHoverIcons_StayOnFloorAndTrackZoom()
        {
            yield return LoadScene(new Vector2(800, 600));
            var controller = Component<DecorationModeController>();
            var previewPrefab = Field<GameObject>(controller, "functionalSurfacePreviewPrefab");
            var sharedFootprint = previewPrefab.transform.Find("Footprint").GetComponent<Renderer>().sharedMaterial;
            var sharedLightIntensity = sharedFootprint.GetFloat("_LightIntensity");
            var sharedOpacity = sharedFootprint.GetFloat("_FootprintOpacity");
            controller.EnterDecorationMode();
            Select("equipment.cash-register.01");
            yield return Settle();
            AssertIndicatorVisible();
            var arrows = Components<MeshRenderer>().Where(item => item.name.EndsWith("RoleArrow")).ToArray();
            Assert.That(arrows.Length, Is.EqualTo(2), "Each role needs a real, floor-projected arrow.");
            var camera = UnityEngine.Camera.main;
            var size = camera.orthographicSize;
            foreach (var zoom in new[] { size, 4f, 12f })
            {
                camera.orthographicSize = zoom;
                yield return Settle();
                Assert.That(camera.orthographicSize, Is.EqualTo(zoom).Within(.01f), "Exercise the actual authored camera zoom range");
                AssertRoleIconsShareArrowRelativeHeight("zoom " + zoom);
                AssertIconsAvoidActionFaces("zoom " + zoom);
                foreach (var arrow in arrows)
                {
                    var role = arrow.name.StartsWith("Employee") ? "Employee" : "Customer";
                    var icon = Box(Child(role + "Badge"));
                    var anchor = camera.WorldToScreenPoint(arrow.transform.position);
                    Assert.That(icon.center.x, Is.EqualTo(anchor.x).Within(1f), role + " must hover directly over its arrow");
                    Assert.That(icon.yMin, Is.GreaterThan(anchor.y + 2), role + " needs visible air below the icon");
                    Assert.That(arrow.bounds.size.y, Is.LessThan(.002f), "Arrow lies flat, not an upright UI badge");
                    Assert.That(arrow.transform.position.y, Is.EqualTo(.025f).Within(.002f), "Arrow clears the floor surface like existing footprints, not the Counter top");
                    Assert.That(arrow.bounds.size.magnitude, Is.LessThan(.75f), "Arrows stay smaller than one floor cell");
                    Assert.That(arrow.sharedMaterial.shader.name, Is.EqualTo("AnimalCafe/Phase8/FootprintLight"));
                    Assert.That(arrow.sharedMaterial, Is.Not.SameAs(sharedFootprint),
                        "Role tint must use a clone, not the shared footprint material");
                    Assert.That(arrow.sharedMaterial.GetFloat("_LightIntensity"), Is.EqualTo(1.3f).Within(.01f));
                    Assert.That(arrow.sharedMaterial.GetFloat("_FootprintOpacity"), Is.EqualTo(.65f).Within(.01f));
                    Assert.That(arrow.GetComponent<Collider>(), Is.Null);
                    Assert.That(arrow.enabled && arrow.gameObject.activeInHierarchy, Is.True);
                }
            }
            Assert.That(sharedFootprint.GetFloat("_LightIntensity"), Is.EqualTo(sharedLightIntensity).Within(.001f),
                "Role-arrow brightness must not mutate the shared placement footprint material");
            Assert.That(sharedFootprint.GetFloat("_FootprintOpacity"), Is.EqualTo(sharedOpacity).Within(.001f),
                "Role-arrow opacity must not mutate the shared placement footprint material");
            Assert.That(IndicatorRoot().GetComponentsInChildren<TMP_Text>(true), Is.Empty,
                "The approved version uses icons, without the old role text cards.");
            Component<DecorationModeController>().CancelFunctionalSurfacePreview();
            yield return null;
            Assert.That(arrows.All(arrow => arrow == null || !arrow.enabled || !arrow.gameObject.activeInHierarchy), Is.True,
                "Cancelling hides world renderers as well as the Overlay CanvasGroup.");
        }

        [UnityTest]
        public IEnumerator CashPreview_FourQuarterTurnsAndRotatedSupport_ProjectNamedEndpointsToDeviceSides()
        {
            yield return LoadScene(new Vector2(800, 600));
            var controller = Component<DecorationModeController>();
            var runtime = Component<CafeLayoutRuntime>();
            controller.EnterDecorationMode();

            var rotatedSupport = FurnitureInstance.CreateNew(
                "furniture.counter.module.01", new GridPosition(5, 4), FurnitureRotation.Degrees90);
            Assert.That(runtime.Layout.PlaceFurniture(rotatedSupport).Succeeded, Is.True);
            Component<FurnitureSceneRegistry>().Rebuild(runtime.Layout.FurnitureInstances);
            Select("equipment.cash-register.01");
            var originalSupport = runtime.Layout.FurnitureInstances.Single(item =>
                item.DefinitionId == "furniture.counter.module.01" && item.Rotation == FurnitureRotation.Degrees0);
            Assert.That(controller.TryMoveFunctionalSurfacePreview(
                new SurfaceSlotAddress(originalSupport.InstanceId, "slot.0")), Is.True);

            // Hand-checked for MainCafe's fixed isometric Camera: North is upper-left.
            // MainCafe固定相机下，North投影为左上；literal避免复制production换算公式。
            var employeeQuadrants = new[]
            {
                new Vector2(-1, 1), new Vector2(1, 1),
                new Vector2(1, -1), new Vector2(-1, -1)
            };
            for (var turn = 0; turn < employeeQuadrants.Length; turn++)
            {
                yield return Settle();
                AssertRoleEndpoints(employeeQuadrants[turn], "equipment turn " + turn);
                if (turn < employeeQuadrants.Length - 1)
                    Assert.That(controller.TryRotateFunctionalSurfacePreview(), Is.True);
            }

            Assert.That(controller.TryRotateFunctionalSurfacePreview(), Is.True); // Back to local Degrees0.
            Assert.That(controller.TryMoveFunctionalSurfacePreview(
                new SurfaceSlotAddress(rotatedSupport.InstanceId, "slot.0")), Is.True);
            yield return Settle();
            AssertRoleEndpoints(new Vector2(1, 1), "support Degrees90 plus equipment Degrees0");

            Assert.That(controller.TryMoveFunctionalSurfacePreview(default), Is.False);
            Assert.That(controller.ActiveFunctionalSurfacePreview.CanConfirm, Is.False);
            yield return Settle();
            AssertRoleEndpoints(new Vector2(-1, 1), "invalid floor fallback still follows the ghost");
        }

        [UnityTest]
        public IEnumerator CashIndicator_NewConfirmExistingCancelAndCoveringTransitions_UsePreviewLifetimeOnly()
        {
            yield return LoadScene(new Vector2(800, 600));
            var controller = Component<DecorationModeController>();
            controller.EnterDecorationMode();
            Select("equipment.cash-register.01");
            yield return Settle();
            AssertIndicatorVisible();

            Assert.That(controller.TryConfirmFunctionalSurfacePreview(), Is.True);
            yield return null;
            AssertIndicatorHidden("Confirm ended the preview");
            var cash = Component<CafeLayoutRuntime>().FunctionalSurfaceLayout.MountedInstances
                .Single(item => item.DefinitionId == "equipment.cash-register.01");
            Assert.That(controller.TryBeginExistingFunctionalSurfacePreview(
                FunctionalSurfacePreviewKind.MountedEquipment, cash.InstanceId), Is.True);
            yield return Settle();
            AssertIndicatorVisible();

            Action("storeButton").onClick.Invoke();
            yield return null;
            Assert.That(Component<DecorationStoreModalView>().IsOpen, Is.True);
            AssertIndicatorHidden("Store modal covers the scene explanation");
            Field<Button>(Component<DecorationStoreModalView>(), "cancelButton").onClick.Invoke();
            yield return Settle();
            AssertIndicatorVisible();

            var catalogue = Component<DecorationCatalogueView>();
            catalogue.ShowCatalogue();
            yield return Settle();
            AssertIndicatorHidden("Expanded catalogue covers the scene explanation");
            Field<Button>(catalogue, "returnToEditingButton").onClick.Invoke();
            yield return Settle();
            AssertIndicatorVisible();

            controller.CancelFunctionalSurfacePreview();
            yield return null;
            AssertIndicatorHidden("Cancel ended the existing preview");
            Assert.That(controller.TryChangeMode(DecorationModeKind.Floor), Is.True);
            AssertIndicatorHidden("Tab switch cannot leave labels behind");
            Assert.That(controller.TryChangeMode(DecorationModeKind.Furniture), Is.True);
            Select("equipment.coffee-machine.01");
            yield return Settle();
            AssertIndicatorHidden("Coffee Machine has no Customer role badge");
            controller.CancelFunctionalSurfacePreview();
            Select("equipment.cash-register.01");
            yield return Settle();
            AssertIndicatorVisible();
            controller.ExitDecorationMode();
            yield return null;
            AssertIndicatorHidden("Decoration exit hides preview-only labels");
        }

        [UnityTest]
        public IEnumerator CashIndicator_PresentationIsReadableAndCannotOwnRaycasts()
        {
            yield return LoadScene(new Vector2(800, 600));
            Component<DecorationModeController>().EnterDecorationMode();
            Select("equipment.cash-register.01");
            yield return Settle();
            var root = IndicatorRoot();
            var group = root.GetComponent<CanvasGroup>();
            Assert.That(group, Is.Not.Null);
            Assert.That(group.blocksRaycasts, Is.False);
            Assert.That(group.interactable, Is.False);
            Assert.That(root.GetComponentsInChildren<Graphic>(true).All(item => !item.raycastTarget), Is.True);
            Assert.That(root.GetComponentsInChildren<Selectable>(true), Is.Empty);
            Assert.That(root.GetComponentsInChildren<Collider>(true), Is.Empty);

            AssertBadge("EmployeeBadge", "Employee", "apron");
            AssertBadge("CustomerBadge", "Customer", "bag");
            foreach (var role in new[] { "Employee", "Customer" })
            {
                var arrow = Components<MeshRenderer>().Single(item => item.name == role + "RoleArrow");
                var mesh = arrow.GetComponent<MeshFilter>().sharedMesh;
                Assert.That(mesh, Is.Not.Null);
                Assert.That(mesh.vertexCount, Is.GreaterThan(7), role + " arrow needs a soft edge rim");
                Assert.That(mesh.uv.Any(uv => uv.x == 0) && mesh.uv.Any(uv => uv.x == .5f), Is.True,
                    "The footprint shader needs outer fading UVs and opaque-center UVs.");
                Assert.That(arrow.GetComponent<Collider>(), Is.Null);
            }

            var probe = Box(Child("EmployeeBadge")).center;
            var withLabels = Raycast(probe);
            Assert.That(withLabels.All(hit => !hit.gameObject.transform.IsChildOf(root.transform)), Is.True,
                "No indicator child may own an EventSystem hit.");
            var view = root.GetComponents<MonoBehaviour>()
                .Single(item => item.GetType().Name == "CashRegisterSideIndicatorView");
            view.enabled = false;
            yield return null;
            AssertIndicatorHidden("Disabling the view hides every owned visual");
            Assert.That(Raycast(probe).Select(hit => hit.gameObject),
                Is.EqualTo(withLabels.Select(hit => hit.gameObject)),
                "Removing labels must not change the underlying UI raycast result.");
        }

        [UnityTest]
        public IEnumerator CashIndicator_SmallPortraitAndLandscape_StayInsideSafeAreaAndAvoidRealCards()
        {
            using (var screen = new P8RReferenceLayoutTests.NativeScreenSize())
            {
                var cases = new[]
                {
                    new { Pixels = new Vector2(480, 854), Logical = new Vector2(320, 569), Safe = new Rect(18, 42, 444, 782) },
                    new { Pixels = new Vector2(1600, 720), Logical = new Vector2(800, 360), Safe = new Rect(72, 18, 1456, 684) }
                };
                foreach (var item in cases)
                {
                    screen.Resize(item.Pixels);
                    yield return LoadScene(item.Logical, item.Safe);
                    Component<DecorationModeController>().EnterDecorationMode();
                    Select("equipment.cash-register.01");
                    yield return Settle();

                    AssertIndicatorVisible();
                    var employee = Box(Child("EmployeeBadge").Find("InvalidOverlay"));
                    var customer = Box(Child("CustomerBadge").Find("InvalidOverlay"));
                    Assert.That(employee.Overlaps(customer), Is.False, "Role badges remain distinct at " + item.Pixels);
                    foreach (var badge in new[] { employee, customer })
                    {
                        Assert.That(item.Safe.Contains(badge.min) && item.Safe.Contains(badge.max), Is.True,
                            "Badge must stay inside the applied safe area at " + item.Pixels + ": " + badge);
                    }

                    var obstacles = Component<DecorationActionBarView>().GetComponentsInChildren<Button>(true)
                        .Where(button => button.gameObject.activeInHierarchy)
                        .Select(button => new { Name = button.name, Rect = Box(button.image != null ? button.image.transform : button.transform) }).ToList();
                    obstacles.Add(new { Name = "CollapsedHandle", Rect = Box(Component<DecorationCatalogueView>().CollapsedHandleRect) });
                    var readiness = Component<ValidationMessageView>();
                    if (readiness.gameObject.activeInHierarchy) obstacles.Add(new { Name = "Readiness", Rect = Box(readiness) });
                    foreach (var badge in new[] { employee, customer })
                        Assert.That(obstacles.All(card => !badge.Overlaps(card.Rect)), Is.True,
                            "Role badge overlaps real preview chrome at " + item.Pixels + ": " + badge
                            + " hits " + string.Join(",", obstacles.Where(card => badge.Overlaps(card.Rect)).Select(card => card.Name + card.Rect)));
                }
            }
        }

        [UnityTest]
        public IEnumerator CashPreview_RealSceneUiAndTableEdgeDrag_KeepsSupportHeight()
        {
            using (var screen = new P8RReferenceLayoutTests.NativeScreenSize())
            {
                screen.Resize(new Vector2(1600, 1200));
                yield return LoadScene(new Vector2(800, 600));
                Component<DecorationModeController>().EnterDecorationMode();
                Select("equipment.cash-register.01");
                foreach (var zoom in new[] { 4f, 12f })
                {
                    UnityEngine.Camera.main.orthographicSize = zoom;
                    yield return Settle();
                    ExerciseCashDragAcrossUiAndTableEdge();
                    yield return Settle();
                    AssertIndicatorVisible();
                    AssertIconsAvoidActionFaces("real drag zoom " + zoom);
                }
            }
        }

        private void ExerciseCashDragAcrossUiAndTableEdge()
        {
            var controller = Component<DecorationModeController>();
            var runtime = Component<CafeLayoutRuntime>();
            var address = controller.ActiveFunctionalSurfacePreview.Address;
            var readiness = runtime.CurrentReadiness;
            var version = runtime.ReadinessVersion;
            var ghostPosition = Component<SurfaceMountedPreviewView>().CurrentGhost.transform.position;
            var floor = Field<Transform>(controller, "gridRoot");
            var center = floor.TransformPoint(new Vector3(2.5f, .72f, 3.5f));
            var edge = floor.TransformPoint(new Vector3(2.94f, .72f, 3.94f));
            var camera = UnityEngine.Camera.main;
            var router = Field<DecorationTouchRouter>(controller, "touchRouter");
            var classifier = (IDecorationTouchHitClassifier)controller;
            // Continue the live router's sequence; stale frame numbers are deliberately ignored.
            // 接续正式 Router 的帧号，不能从 1 重置而被输入去重丢弃。
            var frameBase = Mathf.Max(Time.frameCount, Field<int>(router, "lastProcessedFrame"));
            DecorationTouchRoutingResult Send(Vector2 point, UnityEngine.InputSystem.TouchPhase phase, int sequence)
            {
                var result = router.ProcessFrame(new DecorationTouchFrame(frameBase + sequence, new[] {
                    new DecorationTouchPoint(719, point, Vector2.zero, phase) }), classifier);
                controller.RouteTouchResultForActiveMode(result);
                return result;
            }
            var began = Send(camera.WorldToScreenPoint(center), UnityEngine.InputSystem.TouchPhase.Began, 1);
            Assert.That(began.Owner, Is.EqualTo(DecorationGestureOwner.FunctionalSurface));
            var ui = Box(Action("rotateButton").image).center;
            var crossed = Send(ui, UnityEngine.InputSystem.TouchPhase.Moved, 2);
            Assert.That(crossed.CurrentHit.Kind, Is.EqualTo(DecorationTouchHitKind.Ui));
            Assert.That(controller.ActiveFunctionalSurfacePreview.Address, Is.EqualTo(address));
            Assert.That(Component<SurfaceMountedPreviewView>().CurrentGhost.transform.position, Is.EqualTo(ghostPosition));
            var returned = Send(camera.WorldToScreenPoint(edge), UnityEngine.InputSystem.TouchPhase.Moved, 3);
            Assert.That(returned.CurrentHit.FunctionalSurfaceAddress, Is.EqualTo(address));
            Assert.That(returned.Owner, Is.EqualTo(DecorationGestureOwner.FunctionalSurface));
            Assert.That(Component<SurfaceMountedPreviewView>().CurrentGhost.transform.position, Is.EqualTo(ghostPosition));
            Send(camera.WorldToScreenPoint(edge), UnityEngine.InputSystem.TouchPhase.Ended, 4);
            Assert.That(router.Owner, Is.EqualTo(DecorationGestureOwner.None));
            Assert.That(controller.ActiveFunctionalSurfacePreview, Is.Not.Null);
            Assert.That(runtime.FunctionalSurfaceLayout.MountedInstances, Is.Empty);
            Assert.That(runtime.CurrentReadiness, Is.SameAs(readiness));
            Assert.That(runtime.ReadinessVersion, Is.EqualTo(version));
        }

        [UnityTest]
        public IEnumerator CashIndicator_NativeOverlayCapture_WhenExplicitlyEnabled_WritesUniqueReviewGallery()
        {
            if (Environment.GetEnvironmentVariable("ANIMALCAFE_CASH_SIDE_CAPTURE") != "1")
                Assert.Ignore("Set ANIMALCAFE_CASH_SIDE_CAPTURE=1 for native Cash Register evidence.");
            if (Application.isBatchMode)
                Assert.Ignore("Native Overlay evidence requires a normal Editor GameView.");

            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff");
            var folder = Path.GetFullPath(Path.Combine(
                "outputs", "p8r-cash-side-indicators-20260914", "native-" + timestamp));
            Assert.That(Directory.Exists(folder), Is.False, "Capture evidence must never overwrite an earlier run.");
            Directory.CreateDirectory(folder);

            using (var screen = new P8RReferenceLayoutTests.RealGameViewSize())
            {
                var cases = new[]
                {
                    new { Name = "portrait-480x854", Pixels = new Vector2(480, 854), Logical = new Vector2(320, 569) },
                    new { Name = "landscape-1600x720", Pixels = new Vector2(1600, 720), Logical = new Vector2(800, 360) }
                };
                foreach (var item in cases)
                {
                    screen.Resize(item.Pixels);
                    yield return new WaitForSecondsRealtime(.3f);
                    yield return LoadScene(item.Logical);
                    Assert.That(new Vector2(Screen.width, Screen.height), Is.EqualTo(item.Pixels));
                    Assert.That(new Vector2(UnityEngine.Camera.main.pixelWidth, UnityEngine.Camera.main.pixelHeight),
                        Is.EqualTo(item.Pixels));

                    var controller = Component<DecorationModeController>();
                    controller.EnterDecorationMode();
                    Select("equipment.cash-register.01");
                    yield return Settle();
                    AssertIndicatorVisible();
                    yield return CaptureNative(folder, item.Name + "-cash-0.png");

                    ExerciseCashDragAcrossUiAndTableEdge();
                    yield return Settle();
                    AssertIndicatorVisible();
                    yield return CaptureNative(folder, item.Name + "-drag-ui-and-table-edge.png");

                    var originalZoom = UnityEngine.Camera.main.orthographicSize;
                    foreach (var zoom in new[] { 4f, 12f })
                    {
                        UnityEngine.Camera.main.orthographicSize = zoom;
                        yield return Settle();
                        Assert.That(UnityEngine.Camera.main.orthographicSize, Is.EqualTo(zoom).Within(.01f));
                        AssertIndicatorVisible();
                        yield return CaptureNative(folder, item.Name + "-zoom-" + zoom + ".png");
                    }
                    UnityEngine.Camera.main.orthographicSize = originalZoom;

                    Assert.That(controller.TryRotateFunctionalSurfacePreview(), Is.True);
                    yield return Settle();
                    AssertIndicatorVisible();
                    yield return CaptureNative(folder, item.Name + "-cash-90.png");

                    // Screenshot the reported three-counter obstruction with exact domain anchors.
                    var runtime = Component<CafeLayoutRuntime>();
                    for (var turn = 0; turn < 3; turn++) Assert.That(controller.TryRotateFunctionalSurfacePreview(), Is.True);
                    foreach (var cell in new[] { new GridPosition(2, 4), new GridPosition(2, 2) })
                        Assert.That(runtime.Layout.PlaceFurniture(FurnitureInstance.CreateNew(
                            "furniture.counter.module.01", cell, FurnitureRotation.Degrees0)).Succeeded, Is.True);
                    Component<FurnitureSceneRegistry>().Rebuild(runtime.Layout.FurnitureInstances);
                    var support = runtime.Layout.FurnitureInstances.Single(f => f.Position == new GridPosition(2, 3));
                    Assert.That(controller.TryMoveFunctionalSurfacePreview(new SurfaceSlotAddress(support.InstanceId, "slot.0")), Is.True);
                    UnityEngine.Camera.main.orthographicSize = 4;
                    yield return Settle();
                    AssertInvalidRole("Employee", true);
                    AssertInvalidRole("Customer", true);
                    AssertArrowCell("Employee", 2, 4);
                    AssertArrowCell("Customer", 2, 2);
                    AssertIconsAvoidActionFaces("native blocked roles");
                    yield return CaptureNative(folder, item.Name + "-both-blocked.png");
                    Assert.That(controller.TryRotateFunctionalSurfacePreview(), Is.True);
                    yield return Settle();
                    AssertInvalidRole("Employee", false);
                    AssertInvalidRole("Customer", false);
                    yield return CaptureNative(folder, item.Name + "-rotated-recovered.png");
                    Assert.That(controller.TryConfirmFunctionalSurfacePreview(), Is.True);
                    yield return Settle();
                    AssertIndicatorHidden("Confirmed native frame must contain no preview-only labels");
                    yield return CaptureNative(folder, item.Name + "-confirmed-hidden.png");
                }
            }
        }

        private void AssertRoleEndpoints(Vector2 employeeSigns, string context)
        {
            var ghost = Component<SurfaceMountedPreviewView>().CurrentGhost;
            Assert.That(ghost, Is.Not.Null, context);
            var renderers = ghost.GetComponentsInChildren<Renderer>(true)
                .Where(item => item.enabled && item.gameObject.activeInHierarchy).ToArray();
            Assert.That(renderers, Is.Not.Empty, context);
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            var groundCenter = new Vector3(bounds.center.x, .025f, bounds.center.z);
            var center = (Vector2)UnityEngine.Camera.main.WorldToScreenPoint(groundCenter);
            var employeeArrow = Components<MeshRenderer>().Single(item => item.name == "EmployeeRoleArrow");
            var customerArrow = Components<MeshRenderer>().Single(item => item.name == "CustomerRoleArrow");
            var employee = (Vector2)UnityEngine.Camera.main.WorldToScreenPoint(employeeArrow.transform.position) - center;
            var customer = (Vector2)UnityEngine.Camera.main.WorldToScreenPoint(customerArrow.transform.position) - center;
            foreach (var arrow in new[] { employeeArrow, customerArrow })
                Assert.That(Vector3.Dot(arrow.transform.forward, (groundCenter - arrow.transform.position).normalized),
                    Is.GreaterThan(.99f), context + " arrows point inward toward the device");
            TestContext.WriteLine(context + " alpha=" + IndicatorRoot().GetComponent<CanvasGroup>().alpha
                + " rotation=" + ghost.transform.eulerAngles + " center=" + center
                + " employee=" + employee + " customer=" + customer);
            AssertIndicatorVisible();
            Assert.That(employee.x * employeeSigns.x, Is.GreaterThan(1f), context + " Employee horizontal side");
            Assert.That(employee.y * employeeSigns.y, Is.GreaterThan(1f), context + " Employee vertical side");
            Assert.That(customer.x * employeeSigns.x, Is.LessThan(-1f), context + " Customer horizontal side");
            Assert.That(customer.y * employeeSigns.y, Is.LessThan(-1f), context + " Customer vertical side");
            Assert.That(Vector2.Dot(employee.normalized, customer.normalized), Is.LessThan(-.9f),
                context + " endpoints must remain opposite around the rendered device");
        }

        private void AssertRoleIconsShareArrowRelativeHeight(string context)
        {
            var camera = UnityEngine.Camera.main;
            var employeeArrow = Components<MeshRenderer>().Single(item => item.name == "EmployeeRoleArrow");
            var customerArrow = Components<MeshRenderer>().Single(item => item.name == "CustomerRoleArrow");
            var employeeHeight = Box(Child("EmployeeBadge")).center.y
                - camera.WorldToScreenPoint(employeeArrow.transform.position).y;
            var customerHeight = Box(Child("CustomerBadge")).center.y
                - camera.WorldToScreenPoint(customerArrow.transform.position).y;
            var tolerance = P8RMobileMetrics.For(IndicatorRoot().GetComponent<CashRegisterSideIndicatorView>())
                .PixelsPerLogicalUnit;
            Assert.That(employeeHeight, Is.EqualTo(customerHeight).Within(tolerance),
                context + " role icons must keep the same screen height above their own arrows; Employee="
                + employeeHeight + " Customer=" + customerHeight);
        }

        private void AssertIconsAvoidActionFaces(string context)
        {
            var buttons = Component<DecorationActionBarView>().GetComponentsInChildren<Button>(true)
                .Where(button => button.gameObject.activeInHierarchy && button.image != null);
            foreach (var button in buttons)
            foreach (var role in new[] { "Employee", "Customer" })
                Assert.That(Box(Child(role + "Badge").Find("InvalidOverlay")).Overlaps(Box(button.image)), Is.False,
                    context + ": " + role + " full invalid-ring envelope overlaps " + button.name);
        }

        private void AssertBadge(string name, string expectedText, string iconSemantic)
        {
            var badge = Child(name);
            var icon = badge.GetComponent<Image>();
            Assert.That(icon.sprite, Is.Not.Null);
            Assert.That(icon.sprite.name.ToLowerInvariant(), Does.Contain(iconSemantic));
            Assert.That(icon.sprite.texture.filterMode, Is.EqualTo(FilterMode.Trilinear));
            Assert.That(icon.preserveAspect, Is.True, expectedText + " icon keeps its approved proportions");
            Assert.That(badge.GetComponentsInChildren<TMP_Text>(true), Is.Empty);
        }

        private void AssertIndicatorVisible()
        {
            var root = IndicatorRoot();
            Assert.That(root.activeInHierarchy, Is.True);
            if (root.GetComponent<CanvasGroup>().alpha < .99f)
            {
                var view = root.GetComponent<CashRegisterSideIndicatorView>();
                TestContext.WriteLine("Hidden geometry: screen=" + Screen.width + "x" + Screen.height
                    + " covered=" + Field<bool>(view, "covered")
                    + " actions=" + Box(Component<DecorationActionBarView>().transform.Find("ActionPanel"))
                    + " safe=" + Field<Func<Rect>>(view, "safeArea")());
                foreach (var role in new[] { "Employee", "Customer" })
                {
                    var arrow = Components<MeshRenderer>().Single(item => item.name == role + "RoleArrow");
                    TestContext.WriteLine(role + " icon=" + Box(Child(role + "Badge")) + " arrow=" + arrow.transform.position
                        + " bounds=" + arrow.bounds + " screenAnchor=" + UnityEngine.Camera.main.WorldToScreenPoint(arrow.transform.position));
                }
            }
            Assert.That(root.GetComponent<CanvasGroup>().alpha, Is.GreaterThan(.99f));
            Assert.That(Child("EmployeeBadge").gameObject.activeInHierarchy, Is.True);
            Assert.That(Child("CustomerBadge").gameObject.activeInHierarchy, Is.True);
            Assert.That(Components<MeshRenderer>().Count(item => item.name.EndsWith("RoleArrow")
                && item.enabled && item.gameObject.activeInHierarchy), Is.EqualTo(2));
        }

        private void AssertIndicatorHidden(string reason)
        {
            var root = IndicatorRootOrNull();
            Assert.That(root == null || !root.activeInHierarchy || root.GetComponent<CanvasGroup>().alpha < .01f, Is.True, reason);
            Assert.That(Components<MeshRenderer>().Any(item => item.name.EndsWith("RoleArrow")
                && item.enabled && item.gameObject.activeInHierarchy), Is.False, reason + " (world arrows)");
        }

        private GameObject IndicatorRoot() => IndicatorRootOrNull() ?? throw new AssertionException(
            "CashRegisterSideIndicators must exist while a Cash Register preview is visible.");
        private GameObject IndicatorRootOrNull() => Components<Transform>()
            .FirstOrDefault(item => item.name == "CashRegisterSideIndicators")?.gameObject;
        private Transform Child(string name) => IndicatorRoot().transform.Find(name) ?? throw new AssertionException(name + " is required.");
        private void Select(string id)
        {
            var catalogue = Component<DecorationCatalogueView>();
            if (catalogue.IsCollapsed) catalogue.ShowCatalogue();
            catalogue.GetComponentsInChildren<DecorationCatalogueTileView>(true)
                .First(tile => tile.ItemId == id && tile.gameObject.activeInHierarchy).GetComponent<Button>().onClick.Invoke();
        }
        private Button Action(string field) => Field<Button>(Component<DecorationActionBarView>(), field);
        private static T Field<T>(object owner, string name) => (T)owner.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);

        private IEnumerator LoadScene(Vector2 logical, Rect? safeArea = null)
        {
            yield return UnloadScene();
            P8RMobileMetrics.EditorLogicalViewportOverride = logical;
            ownedScene = EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/MainCafe.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return null; yield return null; yield return null;
            var pixels = new Vector2(Screen.width, Screen.height);
            foreach (var area in Components<SafeAreaContainer>())
            {
                area.AutoApplyRuntimeSafeArea = false;
                area.ApplySafeArea(safeArea ?? new Rect(Vector2.zero, pixels), pixels);
            }
            yield return Settle();
        }

        private IEnumerator UnloadScene()
        {
            if (!ownedScene.IsValid() || !ownedScene.isLoaded) yield break;
            var assets = Phase8SceneInputTestCleanup.CaptureAssets(ownedScene);
            foreach (var controller in Components<DecorationModeController>()) controller.enabled = false;
            SceneManager.SetActiveScene(SceneManager.CreateScene("P8RCashSideIndicatorCleanup"));
            var unload = SceneManager.UnloadSceneAsync(ownedScene);
            while (unload != null && !unload.isDone) yield return null;
            Phase8SceneInputTestCleanup.DisposeReleasedAssets(assets);
            ownedScene = default;
        }

        private T Component<T>() where T : Component => Components<T>().Single();
        private T[] Components<T>() where T : Component => ownedScene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
        private static IEnumerator Settle()
        {
            yield return new WaitForSecondsRealtime(.3f);
            Canvas.ForceUpdateCanvases();
        }
        private static List<RaycastResult> Raycast(Vector2 point)
        {
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, hits);
            return hits;
        }
        private static IEnumerator CaptureNative(string folder, string filename)
        {
            var path = Path.Combine(folder, filename);
            Assert.That(File.Exists(path), Is.False, "Native evidence filename must be new: " + path);
            Canvas.ForceUpdateCanvases();
            var shaderDeadline = Time.realtimeSinceStartup + 45f;
            while (UnityEditor.ShaderUtil.anythingCompiling && Time.realtimeSinceStartup < shaderDeadline) yield return null;
            Assert.That(UnityEditor.ShaderUtil.anythingCompiling, Is.False);
            Assert.That(ComponentCanvas().renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
            Assert.That(UnityEngine.Camera.main.targetTexture, Is.Null);
            yield return null; yield return null;
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(path, 1);
            for (var frame = 0; frame < 120 && !File.Exists(path); frame++) yield return null;
            Assert.That(File.Exists(path), Is.True, "Native Overlay screenshot was not written: " + path);
            Assert.That(new FileInfo(path).Length, Is.GreaterThan(0), "Native Overlay screenshot is empty: " + path);
        }
        private static Canvas ComponentCanvas() => UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)
            .First(canvas => canvas.name == "Canvas" || canvas.isRootCanvas).rootCanvas;
        private static Rect Box(Component component)
        {
            var rect = (RectTransform)component.transform;
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            var canvas = component.GetComponentInParent<Canvas>().rootCanvas;
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var points = corners.Select(point => RectTransformUtility.WorldToScreenPoint(camera, point)).ToArray();
            return Rect.MinMaxRect(points.Min(point => point.x), points.Min(point => point.y),
                points.Max(point => point.x), points.Max(point => point.y));
        }
    }
}
#endif

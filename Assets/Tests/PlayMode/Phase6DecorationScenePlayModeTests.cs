using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using AnimalCafe.Core.Time;
using AnimalCafe.Content;
using AnimalCafe.Decoration;
using AnimalCafe.Decoration.Input;
using AnimalCafe.Interaction;
using AnimalCafe.Layout;
using AnimalCafe.UI;
using AnimalCafe.UI.Components;
using AnimalCafe.UI.Decoration;
using AnimalCafe.UI.Foundation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using CafeCameraController = AnimalCafe.Camera.CafeCameraController;
using CameraSettings = AnimalCafe.Camera.CameraSettings;

namespace AnimalCafe.Tests.PlayMode
{
    public sealed class Phase6DecorationScenePlayModeTests
    {
        private const float Epsilon = 0.0001f;

        private readonly List<UnityEngine.Object> ownedAssets =
            new List<UnityEngine.Object>();
        private readonly List<GameObject> ownedRoots = new List<GameObject>();

        private Material worldMaterial;
        private AnimalCafeUiTheme theme;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            worldMaterial = CreateWorldMaterial();
            theme = ScriptableObject.CreateInstance<AnimalCafeUiTheme>();
            theme.name = "Task4RuntimeTheme";
            theme.Colors = new UiSemanticColorTokens
            {
                Surface = new Color(0.76f, 0.81f, 0.72f, 0.28f),
                Accent = new Color(0.18f, 0.82f, 0.38f, 0.95f),
                Destructive = new Color(0.92f, 0.20f, 0.22f, 0.95f)
            };
            ownedAssets.Add(theme);
            yield return null;
        }

        [Test]
        public void ControllerFixture_ConstructionFailureRestoresExistingUiSystems()
        {
            var eventSystemObject = new GameObject(
                "Task7ExternalEventSystem",
                typeof(EventSystem));
            ownedRoots.Add(eventSystemObject);
            var canvasObject = new GameObject(
                "Task7ExternalCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(GraphicRaycaster));
            ownedRoots.Add(canvasObject);
            var eventSystem = eventSystemObject.GetComponent<EventSystem>();
            var raycaster = canvasObject.GetComponent<GraphicRaycaster>();
            Assert.That(eventSystem.enabled, Is.True);
            Assert.That(raycaster.enabled, Is.True);

            Assert.Throws<InvalidOperationException>(() =>
                new Task7ControllerFixture(
                    worldMaterial,
                    theme,
                    1f,
                    addSecondFurniture: false,
                    failAfterDisablingUiSystems: true));

            Assert.That(eventSystem.enabled, Is.True,
                "A failed fixture constructor must restore external EventSystem state.");
            Assert.That(raycaster.enabled, Is.True,
                "A failed fixture constructor must restore external GraphicRaycaster state.");
        }

        [Test]
        public void CafeLayoutRuntime_InitializePublishesOnlyCompleteCandidate()
        {
            var fixture = CreateBootstrapFixture();
            SetField(fixture.CounterDefinition, "prefab", null);

            Assert.Throws<InvalidOperationException>(() => fixture.Runtime.Initialize());
            Assert.That(fixture.Runtime.Layout, Is.Null);

            SetField(fixture.CounterDefinition, "prefab", fixture.CounterPrefab);
            fixture.Runtime.Initialize();

            Assert.That(fixture.Runtime.Layout, Is.Not.Null);
            Assert.That(fixture.Runtime.Layout.UnlockedRegions, Has.Count.EqualTo(1));
            Assert.That(fixture.Runtime.Layout.Reservations, Has.Count.EqualTo(1));
            Assert.That(fixture.Runtime.Layout.FurnitureInstances, Has.Count.EqualTo(1));
            Assert.That(fixture.Runtime.Layout.OccupiedCellCount, Is.EqualTo(1));
        }

        [Test]
        public void CafeLayoutRuntime_RepeatedInitializeReturnsSameLayoutWithoutDuplicates()
        {
            var fixture = CreateBootstrapFixture();

            fixture.Runtime.Initialize();
            var first = fixture.Runtime.Layout;
            fixture.Runtime.Initialize();

            Assert.That(fixture.Runtime.Layout, Is.SameAs(first));
            Assert.That(first.UnlockedRegions, Has.Count.EqualTo(1));
            Assert.That(first.Reservations, Has.Count.EqualTo(1));
            Assert.That(first.FurnitureInstances, Has.Count.EqualTo(1));
            Assert.That(first.OccupiedCellCount, Is.EqualTo(1));
        }

        [Test]
        public void CafeLayoutRuntime_InitialDefaultsMatchApprovedContentAndEntranceType()
        {
            var fixture = CreateBootstrapFixture();

            fixture.Runtime.Initialize();

            var layout = fixture.Runtime.Layout;
            Assert.That(layout.GridSettings.CellSize, Is.EqualTo(1f));
            Assert.That(layout.UnlockedRegions[0].Origin, Is.EqualTo(new GridPosition(0, 0)));
            Assert.That(layout.UnlockedRegions[0].Size, Is.EqualTo(new GridSize(8, 8)));
            Assert.That(layout.UnlockedRegions[0].ZoneType, Is.EqualTo(LayoutZoneType.Interior));
            Assert.That(layout.Reservations[0].Id, Is.EqualTo("entrance.main"));
            Assert.That(layout.Reservations[0].Type, Is.EqualTo(LayoutReservationType.EntranceClearance));
            Assert.That(layout.Reservations[0].Origin, Is.EqualTo(new GridPosition(3, 0)));
            Assert.That(layout.Reservations[0].Size, Is.EqualTo(new GridSize(2, 2)));
            var initial = layout.FurnitureInstances.Single();
            Assert.That(initial.InstanceId, Is.EqualTo("00000000000000000000000000000001"));
            Assert.That(initial.DefinitionId, Is.EqualTo("furniture.counter.module.01"));
            Assert.That(initial.Position, Is.EqualTo(new GridPosition(2, 3)));
            Assert.That(initial.Rotation, Is.EqualTo(FurnitureRotation.Degrees0));
        }

        [Test]
        public void CafeLayoutRuntime_FailedInitializeCanRetryWithoutPartialLayout()
        {
            var fixture = CreateBootstrapFixture();
            SetField(fixture.Entrance, "originX", 2);

            Assert.Throws<InvalidOperationException>(() => fixture.Runtime.Initialize());
            Assert.That(fixture.Runtime.Layout, Is.Null);

            SetField(fixture.Entrance, "originX", 3);
            Assert.DoesNotThrow(() => fixture.Runtime.Initialize());
            Assert.That(fixture.Runtime.Layout.FurnitureInstances, Has.Count.EqualTo(1));
            Assert.That(fixture.Runtime.Layout.OccupiedCellCount, Is.EqualTo(1));
        }

        [Test]
        public void CafeLayoutRuntime_NewComponentReconstructsApprovedInitialStateWithoutSave()
        {
            var first = CreateBootstrapFixture();
            var secondObject = CreateRoot("CafeLayoutRuntime_Reconstructed");
            var second = secondObject.AddComponent<CafeLayoutRuntime>();
            SetField(second, "contentCatalog", first.Catalog);
            SetField(second, "entrancePortal", first.Entrance);

            first.Runtime.Initialize();
            Assert.That(first.Runtime.Layout.RemoveFurniture(
                "00000000000000000000000000000001").Succeeded, Is.True);
            Assert.That(first.Runtime.Layout.FurnitureInstances, Is.Empty,
                "The first runtime must be observably mutated before reconstruction.");
            second.Initialize();

            Assert.That(second.Layout, Is.Not.SameAs(first.Runtime.Layout));
            Assert.That(second.Layout.FurnitureInstances, Has.Count.EqualTo(1));
            Assert.That(
                second.Layout.FurnitureInstances[0].InstanceId,
                Is.EqualTo("00000000000000000000000000000001"));
            Assert.That(second.Layout.FurnitureInstances[0].Position,
                Is.EqualTo(new GridPosition(2, 3)));
            Assert.That(second.Layout.OccupiedCellCount, Is.EqualTo(1));
        }

        [Test]
        public void Controller_UsesSameProductionCatalogForLayoutRegistryAndStore()
        {
            using var fixture = CreateControllerFixture();

            fixture.Controller.EnterDecorationMode();

            Assert.That(GetField<FurnitureContentCatalog>(fixture.LayoutRuntime, "contentCatalog"),
                Is.SameAs(fixture.Catalog));
            Assert.That(GetField<FurnitureContentCatalog>(fixture.Controller, "contentCatalog"),
                Is.SameAs(fixture.Catalog));
            Assert.That(GetField<FurnitureContentCatalog>(fixture.Registry, "contentCatalog"),
                Is.SameAs(fixture.Catalog));
            fixture.SelectExisting("00000000000000000000000000000001");
            fixture.Action.Store.onClick.Invoke();
            Assert.That(fixture.Modal.View.IsOpen, Is.True);
            Assert.That(fixture.Modal.Body.text, Does.Contain("catalogue"));
        }

        [Test]
        public void Controller_StartupInitializesAndBuildsFormalCounterBeforeEntry()
        {
            using var fixture = CreateStartupControllerFixture();

            Assert.That(fixture.Controller.IsOpen, Is.False);
            Assert.That(fixture.Controller.State, Is.EqualTo(DecorationSessionState.Closed));
            Assert.That(fixture.LayoutRuntime.Layout, Is.Not.Null);
            Assert.That(fixture.LayoutRuntime.Layout.FurnitureInstances, Has.Count.EqualTo(1));
            Assert.That(fixture.Registry.TryGet(
                "00000000000000000000000000000001", out var representation), Is.True);
            Assert.That(representation.activeInHierarchy, Is.True);
            Assert.That(fixture.FormalRoot.Cast<Transform>()
                .Count(child => child.gameObject.activeSelf), Is.EqualTo(1));
            var runtimeGridSpace = GetField<DecorationGridSpace>(
                fixture.Controller,
                "gridSpace");
            Assert.That(runtimeGridSpace.Settings,
                Is.SameAs(fixture.LayoutRuntime.Layout.GridSettings));
            Assert.That(runtimeGridSpace.Bounds.Origin,
                Is.EqualTo(new GridPosition(0, 0)));
            Assert.That(runtimeGridSpace.Bounds.Size,
                Is.EqualTo(new GridSize(8, 8)));
            Assert.That(representation.transform.localPosition,
                Is.EqualTo(runtimeGridSpace.GetCellCenterLocal(new GridPosition(2, 3)))
                    .Using(UnityEngine.TestTools.Utils.Vector3ComparerWithEqualsOperator.Instance));
        }

        [TestCase("layoutRuntime")]
        [TestCase("contentCatalog")]
        [TestCase("catalogueAsset")]
        [TestCase("gameTime")]
        [TestCase("touchSource")]
        [TestCase("mouseSource")]
        [TestCase("targetCamera")]
        [TestCase("cameraSettings")]
        [TestCase("cameraController")]
        [TestCase("sceneInteraction")]
        [TestCase("floorCollider")]
        [TestCase("gridRoot")]
        [TestCase("furnitureRepresentationRoot")]
        [TestCase("furniturePreviewRoot")]
        [TestCase("gridVisualRoot")]
        [TestCase("gridMaterialTemplate")]
        [TestCase("uiTheme")]
        [TestCase("sceneRegistry")]
        [TestCase("previewView")]
        [TestCase("gridView")]
        [TestCase("cameraDriver")]
        [TestCase("catalogueView")]
        [TestCase("actionBarView")]
        [TestCase("storeModalView")]
        [TestCase("decorationModeButton")]
        [TestCase("decorationModeButtonLabel")]
        public void Controller_StartupMissingDependencyStaysClosedWithoutPartialRepresentation(
            string omittedDependency)
        {
            using var fixture = CreateStartupControllerFixture(omittedDependency);

            Assert.That(fixture.Controller.IsOpen, Is.False);
            Assert.That(fixture.Controller.State, Is.EqualTo(DecorationSessionState.Closed));
            Assert.That(fixture.LayoutRuntime.Layout, Is.Null,
                "All bootstrap references must validate before publishing Layout state.");
            Assert.That(fixture.FormalRoot.childCount, Is.Zero);
            Assert.That(fixture.Registry.TryGet(
                "00000000000000000000000000000001", out _), Is.False);
        }

        [Test]
        public void Controller_StartupRejectsMismatchedContentCatalogWithoutPartialRepresentation()
        {
            using var fixture = CreateStartupControllerFixture(
                useMismatchedControllerCatalog: true);

            Assert.That(fixture.Controller.IsOpen, Is.False);
            Assert.That(fixture.LayoutRuntime.Layout, Is.Null);
            Assert.That(fixture.FormalRoot.childCount, Is.Zero);
            Assert.That(fixture.Registry.TryGet(
                "00000000000000000000000000000001", out _), Is.False);
        }

        [Test]
        public void Controller_StartupInactiveFormalRootCannotPublishCompletedBootstrap()
        {
            using var fixture = CreateStartupControllerFixture(
                deactivateFormalRoot: true);

            Assert.Throws<InvalidOperationException>(
                () => fixture.Controller.EnterDecorationMode());
            Assert.That(fixture.Controller.IsOpen, Is.False);
            Assert.That(fixture.Controller.State, Is.EqualTo(DecorationSessionState.Closed));
            Assert.That(fixture.FormalRoot.Cast<Transform>()
                .Count(child => child.gameObject.activeInHierarchy), Is.Zero);
        }

        [Test]
        public void WorldToGrid_CellCentersMapToExpectedCells()
        {
            using var fixture = CreateControllerFixture();

            foreach (var expected in new[]
                     {
                         new GridPosition(0, 0),
                         new GridPosition(3, 5),
                         new GridPosition(7, 7)
                     })
            {
                var world = fixture.GridRoot.TransformPoint(
                    fixture.GridSpace.GetCellCenterLocal(expected));
                Assert.That(fixture.TryProjectWorld(world, out var actual), Is.True);
                Assert.That(actual, Is.EqualTo(expected));
            }
        }

        [Test]
        public void WorldToGrid_ExactBoundaryChoosesPositiveContainingCell()
        {
            using var fixture = CreateControllerFixture();
            var boundary = fixture.GridRoot.TransformPoint(new Vector3(1f, 0f, 1f));

            Assert.That(fixture.TryProjectWorld(boundary, out var actual), Is.True);
            Assert.That(actual, Is.EqualTo(new GridPosition(1, 1)));
        }

        [Test]
        public void WorldToGrid_JustBelowAndAboveBoundaryAreDeterministic()
        {
            using var fixture = CreateControllerFixture();

            Assert.That(fixture.TryProjectWorld(
                fixture.GridRoot.TransformPoint(new Vector3(0.9999f, 0f, 0.9999f)),
                out var below), Is.True);
            Assert.That(fixture.TryProjectWorld(
                fixture.GridRoot.TransformPoint(new Vector3(1.0001f, 0f, 1.0001f)),
                out var above), Is.True);
            Assert.That(below, Is.EqualTo(new GridPosition(0, 0)));
            Assert.That(above, Is.EqualTo(new GridPosition(1, 1)));
        }

        [Test]
        public void WorldToGrid_UsesConfiguredRootInverseTransformAndCellSize()
        {
            using var fixture = CreateControllerFixture(cellSize: 2f);
            fixture.GridRoot.position = new Vector3(11f, 0f, -7f);
            fixture.GridRoot.rotation = Quaternion.Euler(0f, 90f, 0f);
            fixture.GridRoot.localScale = new Vector3(1.5f, 1f, 0.75f);
            var world = fixture.GridRoot.TransformPoint(new Vector3(3.9f, 0f, 4.1f));

            Assert.That(fixture.TryProjectWorld(world, out var actual), Is.True);
            Assert.That(actual, Is.EqualTo(new GridPosition(1, 2)));
        }

        [Test]
        public void WorldToGrid_ParallelOrBehindPlaneReturnsNoHit()
        {
            using var fixture = CreateControllerFixture();

            Assert.That(fixture.TryProjectRay(
                new Ray(new Vector3(1f, 2f, 1f), Vector3.right), out _), Is.False);
            Assert.That(fixture.TryProjectRay(
                new Ray(new Vector3(1f, -2f, 1f), Vector3.down), out _), Is.False);
        }

        [UnityTest]
        public IEnumerator HitClassifier_UiWinsOverPreviewAndFormalFurniture()
        {
            using var fixture = CreateControllerFixture();
            fixture.Controller.EnterDecorationMode();
            fixture.Session.BeginNew(
                "furniture.counter.preset.1x2",
                new GridPosition(5, 4));
            Assert.That(
                fixture.Session.MovePreview(new GridPosition(2, 3)).FailureReason,
                Is.EqualTo(PlacementFailureReason.Overlap));
            fixture.Registry.Rebuild(fixture.Layout.FurnitureInstances);
            var screen = fixture.ScreenForCell(new GridPosition(2, 3));
            fixture.ShowUiOverlay(screen);
            yield return null;

            var hit = fixture.Classify(screen);

            Assert.That(hit.Kind, Is.EqualTo(DecorationTouchHitKind.Ui));
            Assert.That(hit.FurnitureInstanceId, Is.Null);
        }

        [Test]
        public void HitClassifier_OpenModalOwnsPrimaryBeganBeforeLowerLayers()
        {
            using var fixture = CreateControllerFixture();
            fixture.Controller.EnterDecorationMode();
            fixture.SelectExisting("00000000000000000000000000000001");
            fixture.Action.Store.onClick.Invoke();
            Assert.That(fixture.Modal.View.IsOpen, Is.True);
            var preview = fixture.Session.ActivePreview;
            var cameraPosition = fixture.Camera.transform.position;
            var screen = fixture.ScreenForCell(preview.ProposedPosition);

            fixture.SendTouch(
                73,
                screen,
                Vector2.zero,
                UnityEngine.InputSystem.TouchPhase.Began);
            var router = GetField<DecorationTouchRouter>(fixture.Controller, "touchRouter");

            Assert.That(router.Owner, Is.EqualTo(DecorationGestureOwner.Ui));
            Assert.That(fixture.Controller.State,
                Is.EqualTo(DecorationSessionState.ConfirmingStore));
            Assert.That(fixture.Session.ActivePreview, Is.SameAs(preview));
            Assert.That(fixture.Camera.transform.position, Is.EqualTo(cameraPosition));
            Assert.That(fixture.Modal.View.IsOpen, Is.True);
        }

        [Test]
        public void HitClassifier_NewPreviewWithoutColliderOwnsFurnitureGesture()
        {
            using var fixture = CreateControllerFixture();
            fixture.Controller.EnterDecorationMode();
            fixture.Session.BeginNew(
                "furniture.counter.preset.1x2",
                new GridPosition(5, 4));
            var screen = fixture.ScreenForCell(new GridPosition(5, 5));

            var hit = fixture.Classify(screen);

            Assert.That(hit.Kind, Is.EqualTo(DecorationTouchHitKind.Furniture));
            Assert.That(hit.FurnitureInstanceId, Is.Null);
        }

        [Test]
        public void HitClassifier_ActivePreviewTabletopOwnsFurnitureGestureAtIsometricAngle()
        {
            using var fixture = CreateControllerFixture();
            fixture.Controller.EnterDecorationMode();
            fixture.SelectCatalogue(0);
            var preview = fixture.Session.ActivePreview;
            Assert.That(preview, Is.Not.Null);
            Assert.That(fixture.Preview.TryGetWorldBounds(out var bounds), Is.True);

            var floorCenter = fixture.GridRoot.TransformPoint(new Vector3(4f, 0f, 4f));
            fixture.Camera.transform.position = floorCenter + new Vector3(0f, 6f, -6f);
            fixture.Camera.transform.LookAt(floorCenter);
            var tabletopScreen = (Vector2)fixture.Camera.WorldToScreenPoint(
                new Vector3(bounds.center.x, bounds.max.y, bounds.center.z));

            Assert.That(fixture.ProjectScreen(tabletopScreen),
                Is.Not.EqualTo(preview.ProposedPosition),
                "The fixture must reproduce the elevated tabletop-to-floor projection offset.");

            var hit = fixture.Classify(tabletopScreen);

            Assert.That(hit.Kind, Is.EqualTo(DecorationTouchHitKind.Furniture));
            Assert.That(hit.FurnitureInstanceId, Is.Null);
        }

        [Test]
        public void HitClassifier_HiddenExistingUsesPreviewFootprint()
        {
            using var fixture = CreateControllerFixture();
            fixture.Controller.EnterDecorationMode();
            fixture.SelectExisting("00000000000000000000000000000001");
            Assert.That(fixture.Registry.TryGet(
                "00000000000000000000000000000001", out var formal), Is.True);
            Assert.That(formal.activeSelf, Is.False);

            var hit = fixture.Classify(fixture.ScreenForCell(new GridPosition(2, 3)));

            Assert.That(hit.Kind, Is.EqualTo(DecorationTouchHitKind.Furniture));
            Assert.That(hit.FurnitureInstanceId,
                Is.EqualTo("00000000000000000000000000000001"));
        }

        [Test]
        public void HitClassifier_ActivePreviewWinsOverUnderlyingFormalOverlap()
        {
            using var fixture = CreateControllerFixture();
            fixture.Controller.EnterDecorationMode();
            fixture.Session.BeginNew(
                "furniture.counter.preset.1x2",
                new GridPosition(5, 4));
            Assert.That(
                fixture.Session.MovePreview(new GridPosition(2, 3)).FailureReason,
                Is.EqualTo(PlacementFailureReason.Overlap));
            fixture.Registry.Rebuild(fixture.Layout.FurnitureInstances);

            var hit = fixture.Classify(fixture.ScreenForCell(new GridPosition(2, 3)));

            Assert.That(hit.Kind, Is.EqualTo(DecorationTouchHitKind.Furniture));
            Assert.That(hit.FurnitureInstanceId, Is.Null,
                "The active new Preview footprint must win over the formal Counter below it.");
        }

        [Test]
        public void HitClassifier_DistinctVisibleFormalReturnsStableInstanceId()
        {
            using var fixture = CreateControllerFixture();
            fixture.Controller.EnterDecorationMode();
            fixture.Registry.Rebuild(fixture.Layout.FurnitureInstances);

            var hit = fixture.Classify(fixture.ScreenForCell(new GridPosition(2, 3)));

            Assert.That(hit.Kind, Is.EqualTo(DecorationTouchHitKind.Furniture));
            Assert.That(hit.FurnitureInstanceId,
                Is.EqualTo("00000000000000000000000000000001"));
        }

        [Test]
        public void HitClassifier_FormalFurnitureWinsOverFloorBehindIt()
        {
            using var fixture = CreateControllerFixture();
            fixture.Controller.EnterDecorationMode();
            fixture.Registry.Rebuild(fixture.Layout.FurnitureInstances);
            var screen = fixture.ScreenForCell(new GridPosition(2, 3));

            var hit = fixture.Classify(screen);

            Assert.That(hit.Kind, Is.EqualTo(DecorationTouchHitKind.Furniture));
            Assert.That(hit.FurnitureInstanceId,
                Is.EqualTo("00000000000000000000000000000001"));
        }

        [Test]
        public void HitClassifier_AdjacentFurnitureUsesTheVisibleCellsFormalOwnerBeforeAnOverlappingCollider()
        {
            using var fixture = CreateControllerFixture();
            fixture.Controller.EnterDecorationMode();
            var largeId = "00000000000000000000000000000030";
            Assert.That(fixture.Layout.PlaceFurniture(FurnitureInstance.Restore(
                largeId,
                "furniture.counter.preset.2x3",
                new GridPosition(3, 3),
                FurnitureRotation.Degrees0)).Succeeded, Is.True);
            fixture.Registry.Rebuild(fixture.Layout.FurnitureInstances);
            Assert.That(fixture.Registry.TryGet(largeId, out var largeRepresentation), Is.True);

            var overlappingCollider = GameObject.CreatePrimitive(PrimitiveType.Cube);
            overlappingCollider.name = "LargeCounterOverlappingHitbox";
            overlappingCollider.transform.SetParent(largeRepresentation.transform, true);
            overlappingCollider.transform.position = fixture.GridRoot.TransformPoint(
                fixture.GridSpace.GetCellCenterLocal(new GridPosition(2, 3)))
                + fixture.GridRoot.up * 1.25f;
            overlappingCollider.transform.localScale = new Vector3(0.8f, 0.4f, 0.8f);
            overlappingCollider.GetComponent<Renderer>().enabled = false;
            Physics.SyncTransforms();

            var hit = fixture.Classify(fixture.ScreenForCell(new GridPosition(2, 3)));

            Assert.That(hit.Kind, Is.EqualTo(DecorationTouchHitKind.Furniture));
            Assert.That(hit.FurnitureInstanceId,
                Is.EqualTo("00000000000000000000000000000001"),
                "The logical owner of the clicked 1x1 cell must win over a neighbouring large collider.");
        }

        [Test]
        public void HitClassifier_IsometricTabletopSelectsTheVisibleSmallFurnitureBeforeTheFloorCellBehindIt()
        {
            using var fixture = CreateControllerFixture();
            fixture.Controller.EnterDecorationMode();
            var largeId = "00000000000000000000000000000031";
            Assert.That(fixture.Layout.PlaceFurniture(FurnitureInstance.Restore(
                largeId,
                "furniture.counter.preset.2x3",
                new GridPosition(2, 4),
                FurnitureRotation.Degrees0)).Succeeded, Is.True);
            fixture.Registry.Rebuild(fixture.Layout.FurnitureInstances);
            Assert.That(fixture.Registry.TryGet(
                "00000000000000000000000000000001", out var smallRepresentation), Is.True);

            var floorCenter = fixture.GridRoot.TransformPoint(new Vector3(4f, 0f, 4f));
            fixture.Camera.transform.position = floorCenter + new Vector3(0f, 6f, -6f);
            fixture.Camera.transform.LookAt(floorCenter);
            Physics.SyncTransforms();
            var smallBounds = smallRepresentation
                .GetComponentsInChildren<Renderer>(true)
                .Select(renderer => renderer.bounds)
                .Aggregate((combined, next) =>
                {
                    combined.Encapsulate(next);
                    return combined;
                });
            var tabletopScreen = (Vector2)fixture.Camera.WorldToScreenPoint(
                new Vector3(smallBounds.center.x, smallBounds.max.y, smallBounds.center.z));
            var projectedFloorCell = fixture.ProjectScreen(tabletopScreen);
            Assert.That(fixture.Layout.TryGetOccupant(projectedFloorCell, out var projectedOwner),
                Is.True);
            Assert.That(projectedOwner, Is.EqualTo(largeId),
                "The fixture must reproduce the tabletop-to-floor selection offset into the larger neighbour.");

            var hit = fixture.Classify(tabletopScreen);

            Assert.That(hit.Kind, Is.EqualTo(DecorationTouchHitKind.Furniture));
            Assert.That(hit.FurnitureInstanceId,
                Is.EqualTo("00000000000000000000000000000001"));
        }

        [Test]
        public void HitClassifier_ChoosesNearestVisibleFormalHitDeterministically()
        {
            using var fixture = CreateControllerFixture();
            fixture.Controller.EnterDecorationMode();
            var lowerId = "00000000000000000000000000000010";
            var higherId = "00000000000000000000000000000020";
            var lower = FurnitureInstance.Restore(
                lowerId,
                "furniture.counter.module.01",
                new GridPosition(4, 4),
                FurnitureRotation.Degrees0);
            var higher = FurnitureInstance.Restore(
                higherId,
                "furniture.counter.module.01",
                new GridPosition(4, 4),
                FurnitureRotation.Degrees0);
            fixture.Registry.Rebuild(new[] { higher, lower });
            Assert.That(fixture.Registry.TryGet(lowerId, out var lowerObject), Is.True);
            Assert.That(fixture.Registry.TryGet(higherId, out var higherObject), Is.True);
            higherObject.transform.position += Vector3.up * 0.5f;
            Physics.SyncTransforms();
            var screen = fixture.ScreenForCell(new GridPosition(4, 4));

            Assert.That(fixture.Classify(screen).FurnitureInstanceId, Is.EqualTo(higherId),
                "The nearer registered hit must win even when its stable ID sorts later.");

            higherObject.transform.position = lowerObject.transform.position;
            Physics.SyncTransforms();
            Assert.That(fixture.Classify(screen).FurnitureInstanceId, Is.EqualTo(lowerId),
                "An exact distance tie must use stable Instance ID ordinal order.");
        }

        [Test]
        public void HitClassifier_MultipleChildCollidersReturnOneStableInstance()
        {
            using var fixture = CreateControllerFixture();
            fixture.Controller.EnterDecorationMode();
            fixture.Registry.Rebuild(fixture.Layout.FurnitureInstances);
            Assert.That(fixture.Registry.TryGet(
                "00000000000000000000000000000001", out var representation), Is.True);
            var secondCollider = GameObject.CreatePrimitive(PrimitiveType.Cube);
            secondCollider.name = "SecondCollider";
            secondCollider.transform.SetParent(representation.transform, false);
            secondCollider.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            secondCollider.transform.localScale = Vector3.one * 0.4f;
            Physics.SyncTransforms();

            var hit = fixture.Classify(fixture.ScreenForCell(new GridPosition(2, 3)));

            Assert.That(hit.Kind, Is.EqualTo(DecorationTouchHitKind.Furniture));
            Assert.That(hit.FurnitureInstanceId,
                Is.EqualTo("00000000000000000000000000000001"));
        }

        [Test]
        public void HitClassifier_NoFormalHitFallsBackOnlyToConfiguredScene()
        {
            using var fixture = CreateControllerFixture();
            fixture.Controller.EnterDecorationMode();
            fixture.Registry.Rebuild(Array.Empty<FurnitureInstance>());
            var unrelated = GameObject.CreatePrimitive(PrimitiveType.Cube);
            unrelated.name = "UnconfiguredCollider";
            unrelated.transform.position = fixture.GridRoot.TransformPoint(
                fixture.GridSpace.GetCellCenterLocal(new GridPosition(6, 6), 1f));
            fixture.Track(unrelated);
            Physics.SyncTransforms();
            var screen = fixture.ScreenForCell(new GridPosition(6, 6));

            Assert.That(fixture.Classify(screen).Kind, Is.EqualTo(DecorationTouchHitKind.Scene));

            fixture.FloorCollider.enabled = false;
            Assert.That(fixture.Classify(screen).Kind, Is.EqualTo(DecorationTouchHitKind.None));
        }

        [Test]
        public void NewPreview_StartsAtNearestCameraCenterCell()
        {
            using var fixture = CreateControllerFixture();
            fixture.SetCameraFloorPoint(fixture.GridRoot.TransformPoint(new Vector3(4.5f, 0f, 4.5f)));
            fixture.Controller.EnterDecorationMode();

            fixture.SelectCatalogue(1);

            Assert.That(fixture.Controller.State,
                Is.EqualTo(DecorationSessionState.PreviewingNewFurniture));
            Assert.That(fixture.Session.ActivePreview.DefinitionId,
                Is.EqualTo("furniture.counter.preset.1x2"));
            Assert.That(fixture.Session.ActivePreview.ProposedPosition,
                Is.EqualTo(new GridPosition(4, 4)));
            AssertCatalogueCollapsedForPreview(fixture);
            Assert.That(fixture.Action.View.IsVisible, Is.True);
        }

        [Test]
        public void NewPreview_InvalidCameraCenterUsesTheNearestValidEmptyCell()
        {
            using var fixture = CreateControllerFixture();
            fixture.SetCameraFloorPoint(fixture.GridRoot.TransformPoint(new Vector3(3.5f, 0f, 0.5f)));
            fixture.Controller.EnterDecorationMode();

            fixture.SelectCatalogue(0);

            Assert.That(fixture.Session.ActivePreview.ProposedPosition,
                Is.EqualTo(new GridPosition(2, 0)));
            Assert.That(fixture.Session.ActivePreview.PlacementResult.Succeeded, Is.True);
            Assert.That(fixture.Action.Confirm.interactable, Is.True);
            Assert.That(fixture.PreviewRoot.childCount, Is.EqualTo(1));
        }

        [Test]
        public void FurnitureBegan_SelectsDistinctFurnitureAndRestoresPriorPreview()
        {
            using var fixture = CreateControllerFixture(addSecondFurniture: true);
            fixture.Controller.EnterDecorationMode();
            fixture.SelectExisting("00000000000000000000000000000001");
            Assert.That(fixture.Session.MovePreview(new GridPosition(1, 5)).Succeeded, Is.True);

            fixture.SelectExisting("00000000000000000000000000000002");

            Assert.That(fixture.Session.ActivePreview.SourceInstanceId,
                Is.EqualTo("00000000000000000000000000000002"));
            Assert.That(fixture.Layout.TryGetFurnitureInstance(
                "00000000000000000000000000000001", out var first), Is.True);
            Assert.That(first.Position, Is.EqualTo(new GridPosition(2, 3)));
            Assert.That(fixture.Registry.TryGet(first.InstanceId, out var firstFormal), Is.True);
            Assert.That(firstFormal.activeSelf, Is.True);
            Assert.That(fixture.Registry.TryGet(
                "00000000000000000000000000000002", out var secondFormal), Is.True);
            Assert.That(secondFormal.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator FurnitureBegan_SelectsExistingWhileNewPreviewIsActive()
        {
            using var fixture = CreateControllerFixture();
            fixture.Controller.EnterDecorationMode();
            fixture.SelectCatalogue(1);
            var newPreview = fixture.Session.ActivePreview;
            Assert.That(newPreview.IsNew, Is.True);
            var layoutCount = fixture.Layout.FurnitureInstances.Count;
            var existingScreen = fixture.ScreenForCell(new GridPosition(2, 3));

            fixture.SendTouch(
                74,
                existingScreen,
                Vector2.zero,
                UnityEngine.InputSystem.TouchPhase.Began);

            Assert.That(fixture.Layout.FurnitureInstances, Has.Count.EqualTo(layoutCount));
            Assert.That(fixture.Controller.State,
                Is.EqualTo(DecorationSessionState.EditingExistingFurniture));
            Assert.That(fixture.Session.ActivePreview, Is.Not.SameAs(newPreview));
            Assert.That(fixture.Session.ActivePreview.IsNew, Is.False);
            Assert.That(fixture.Session.ActivePreview.SourceInstanceId,
                Is.EqualTo("00000000000000000000000000000001"));
            Assert.That(fixture.Registry.TryGet(
                "00000000000000000000000000000001", out var formal), Is.True);
            Assert.That(formal.activeSelf, Is.False);
            Assert.That(fixture.ActivePreviewObjectCount, Is.EqualTo(1));
            yield return null;
            Assert.That(fixture.PreviewRoot.childCount, Is.EqualTo(1));
        }

        [Test]
        public void FurnitureDragReleaseOverOtherFurnitureDoesNotSwitchSelection()
        {
            using var fixture = CreateControllerFixture(addSecondFurniture: true);
            fixture.Controller.EnterDecorationMode();
            var firstScreen = fixture.ScreenForCell(new GridPosition(2, 3));
            var secondScreen = fixture.ScreenForCell(new GridPosition(6, 5));

            fixture.SendTouch(11, firstScreen, Vector2.zero, UnityEngine.InputSystem.TouchPhase.Began);
            fixture.SendTouch(11, firstScreen + Vector2.right * 40f, Vector2.right * 40f,
                UnityEngine.InputSystem.TouchPhase.Moved);
            fixture.SendTouch(11, secondScreen, secondScreen - firstScreen,
                UnityEngine.InputSystem.TouchPhase.Ended);

            Assert.That(fixture.Session.ActivePreview.SourceInstanceId,
                Is.EqualTo("00000000000000000000000000000001"));
        }

        [Test]
        public void BlankTapWithActivePreviewDoesNotCancelOrMove()
        {
            using var fixture = CreateControllerFixture();
            fixture.Controller.EnterDecorationMode();
            fixture.SelectCatalogue(0);
            var before = fixture.Session.ActivePreview;
            var blank = fixture.ScreenForCell(new GridPosition(7, 7));

            fixture.SendTouch(21, blank, Vector2.zero, UnityEngine.InputSystem.TouchPhase.Began);
            fixture.SendTouch(21, blank, Vector2.zero, UnityEngine.InputSystem.TouchPhase.Ended);

            Assert.That(fixture.Session.ActivePreview, Is.SameAs(before));
            Assert.That(fixture.Controller.State,
                Is.EqualTo(DecorationSessionState.PreviewingNewFurniture));
        }

        [Test]
        public void BlankTapWithoutPreviewClearsOrdinarySelectionOnly()
        {
            using var fixture = CreateControllerFixture();
            fixture.Registry.Rebuild(fixture.Layout.FurnitureInstances);
            Assert.That(fixture.Registry.TryGet(
                "00000000000000000000000000000001", out var formal), Is.True);
            var selectable = formal.GetComponentInChildren<Task4Selectable>();
            Assert.That(selectable, Is.Not.Null);
            Assert.That(fixture.SceneInteraction.TrySelectAt(
                fixture.Camera.WorldToScreenPoint(selectable.transform.position)), Is.True);
            Assert.That(fixture.SceneInteraction.CurrentSelection, Is.SameAs(selectable));
            fixture.Controller.EnterDecorationMode();
            Assert.That(fixture.SceneInteraction.CurrentSelection, Is.SameAs(selectable),
                "Entering Decoration mode must not make the later blank-tap assertion vacuous.");
            var blank = fixture.ScreenForCell(new GridPosition(7, 7));

            fixture.SendTouch(22, blank, Vector2.zero, UnityEngine.InputSystem.TouchPhase.Began);
            fixture.SendTouch(22, blank, Vector2.zero, UnityEngine.InputSystem.TouchPhase.Ended);

            Assert.That(fixture.SceneInteraction.CurrentSelection, Is.Null);
            Assert.That(fixture.Controller.State,
                Is.EqualTo(DecorationSessionState.BrowsingCatalogue));
            Assert.That(fixture.Catalogue.View.IsCatalogueVisible, Is.True);
        }

        [Test]
        public void Controller_StateView_EnterShowsBrowsingCatalogueAndGrid()
        {
            using var fixture = CreateControllerFixture();

            fixture.Controller.EnterDecorationMode();

            Assert.That(fixture.Controller.IsOpen, Is.True);
            Assert.That(fixture.Controller.State, Is.EqualTo(DecorationSessionState.BrowsingCatalogue));
            Assert.That(fixture.Catalogue.View.IsCatalogueVisible, Is.True);
            Assert.That(fixture.Catalogue.View.IsCollapsed, Is.False);
            Assert.That(fixture.Action.View.IsVisible, Is.False);
            Assert.That(fixture.Modal.View.IsOpen, Is.False);
            Assert.That(fixture.ActiveGridCellCount, Is.EqualTo(64));
            Assert.That(fixture.ActiveFootprintCellCount, Is.Zero);
        }

        [Test]
        public void Controller_CallbackGuardsRejectClosedAndNoPreviewOperations()
        {
            using var fixture = CreateControllerFixture();
            var original = fixture.Layout.FurnitureInstances.Single();

            InvokePrivate(fixture.Controller, "HandleCatalogueSelected", fixture.Definitions[0]);
            InvokePrivate(fixture.Controller, "HandleFurnitureBegan", original.InstanceId);
            InvokePrivate(fixture.Controller, "ApplyPreviewMove", new GridPosition(5, 5));
            InvokePrivate(fixture.Controller, "HandleRotateRequested");
            InvokePrivate(fixture.Controller, "HandleConfirmRequested");
            InvokePrivate(fixture.Controller, "HandleCancelRequested");
            InvokePrivate(fixture.Controller, "HandleStoreRequested");
            InvokePrivate(fixture.Controller, "HandleStoreConfirmRequested");
            InvokePrivate(fixture.Controller, "HandleStoreDismissRequested");

            Assert.That(fixture.Controller.State, Is.EqualTo(DecorationSessionState.Closed));
            Assert.That(GetField<DecorationSession>(fixture.Controller, "session"), Is.Null);
            Assert.That(fixture.Layout.FurnitureInstances.Single(), Is.SameAs(original));
            fixture.AssertClosedAndClean();

            fixture.Controller.EnterDecorationMode();
            InvokePrivate(fixture.Controller, "ApplyPreviewMove", new GridPosition(5, 5));
            InvokePrivate(fixture.Controller, "HandleRotateRequested");
            InvokePrivate(fixture.Controller, "HandleConfirmRequested");
            InvokePrivate(fixture.Controller, "HandleCancelRequested");
            InvokePrivate(fixture.Controller, "HandleStoreRequested");
            InvokePrivate(fixture.Controller, "HandleStoreConfirmRequested");
            InvokePrivate(fixture.Controller, "HandleStoreDismissRequested");

            Assert.That(fixture.Controller.State,
                Is.EqualTo(DecorationSessionState.BrowsingCatalogue));
            Assert.That(fixture.Session.ActivePreview, Is.Null);
            Assert.That(fixture.Layout.FurnitureInstances.Single(), Is.SameAs(original));
            Assert.That(fixture.Catalogue.View.IsCatalogueVisible, Is.True);
            Assert.That(fixture.Action.View.IsVisible, Is.False);
            Assert.That(fixture.Modal.View.IsOpen, Is.False);
        }

        [Test]
        public void Controller_CallbackGuardsRejectNonModalOperationsWhileConfirmingStore()
        {
            using var fixture = CreateControllerFixture(addSecondFurniture: true);
            fixture.Controller.EnterDecorationMode();
            fixture.SelectExisting("00000000000000000000000000000001");
            fixture.Action.Store.onClick.Invoke();
            var preview = fixture.Session.ActivePreview;
            var layoutSnapshot = fixture.Layout.FurnitureInstances.ToArray();

            InvokePrivate(fixture.Controller, "HandleCatalogueSelected", fixture.Definitions[1]);
            InvokePrivate(
                fixture.Controller,
                "HandleFurnitureBegan",
                "00000000000000000000000000000002");
            InvokePrivate(fixture.Controller, "ApplyPreviewMove", new GridPosition(5, 5));
            InvokePrivate(fixture.Controller, "HandleRotateRequested");
            InvokePrivate(fixture.Controller, "HandleConfirmRequested");
            InvokePrivate(fixture.Controller, "HandleCancelRequested");
            InvokePrivate(fixture.Controller, "HandleStoreRequested");

            Assert.That(fixture.Controller.State,
                Is.EqualTo(DecorationSessionState.ConfirmingStore));
            Assert.That(fixture.Session.ActivePreview, Is.SameAs(preview));
            Assert.That(fixture.Layout.FurnitureInstances,
                Is.EqualTo(layoutSnapshot).AsCollection);
            Assert.That(fixture.Modal.View.IsOpen, Is.True);
            Assert.That(fixture.Action.View.IsVisible, Is.True);
        }

        [Test]
        public void Controller_StateView_ValidTileShowsNewPreviewAndActionBar()
        {
            using var fixture = CreateControllerFixture();
            fixture.Controller.EnterDecorationMode();

            fixture.SelectCatalogue(0);

            Assert.That(fixture.Controller.State,
                Is.EqualTo(DecorationSessionState.PreviewingNewFurniture));
            AssertCatalogueCollapsedForPreview(fixture);
            Assert.That(fixture.Action.View.IsVisible, Is.True);
            Assert.That(fixture.Action.Store.gameObject.activeSelf, Is.False);
            Assert.That(fixture.PreviewRoot.childCount, Is.EqualTo(1));
            Assert.That(fixture.ActiveFootprintCellCount, Is.EqualTo(1));
        }

        [Test]
        public void Controller_ExistingPreviewCollapsesCatalogueButStoreModalHidesIt()
        {
            using var fixture = CreateControllerFixture();
            fixture.Controller.EnterDecorationMode();

            fixture.SelectExisting("00000000000000000000000000000001");

            Assert.That(fixture.Controller.State,
                Is.EqualTo(DecorationSessionState.EditingExistingFurniture));
            AssertCatalogueCollapsedForPreview(fixture);
            Assert.That(fixture.Modal.View.IsOpen, Is.False);

            fixture.Action.Store.onClick.Invoke();

            Assert.That(fixture.Controller.State,
                Is.EqualTo(DecorationSessionState.ConfirmingStore));
            Assert.That(fixture.Catalogue.View.State,
                Is.EqualTo(DecorationCatalogueState.Hidden));
            Assert.That(fixture.Catalogue.View.IsCatalogueVisible, Is.False);
            Assert.That(fixture.Catalogue.Expanded.activeSelf, Is.False);
            Assert.That(fixture.Catalogue.Collapsed.activeSelf, Is.False);
            Assert.That(fixture.Modal.View.IsOpen, Is.True);
        }

        [Test]
        public void Controller_CatalogueVisibilityOwnsActionBarWhilePreviewRemainsActive()
        {
            using var fixture = CreateControllerFixture();
            fixture.Controller.EnterDecorationMode();
            fixture.SelectExisting("00000000000000000000000000000001");
            var preview = fixture.Session.ActivePreview;

            var expand = fixture.Catalogue.Collapsed
                .GetComponentsInChildren<Button>(true)
                .Single(button => button.name == "CollapsedHandleButton");
            expand.onClick.Invoke();

            Assert.That(fixture.Catalogue.View.State,
                Is.EqualTo(DecorationCatalogueState.Expanded));
            Assert.That(fixture.Action.View.IsVisible, Is.False,
                "Expanding the Catalogue must hide the furniture action menu.");
            Assert.That(fixture.Session.ActivePreview, Is.SameAs(preview));

            var collapse = fixture.Catalogue.Expanded
                .GetComponentsInChildren<Button>(true)
                .Single(button => button.name == "CollapseButton");
            collapse.onClick.Invoke();

            Assert.That(fixture.Catalogue.View.State,
                Is.EqualTo(DecorationCatalogueState.Collapsed));
            Assert.That(fixture.Action.View.IsVisible, Is.True,
                "Collapsing the Catalogue must restore the active preview actions.");
            Assert.That(fixture.Session.ActivePreview, Is.SameAs(preview));
        }

        [UnityTest]
        public IEnumerator Controller_ActionSafeAreaStopsBeforeRightRail()
        {
            var cameraObject = new GameObject("ActionSafeAreaCamera");
            var controllerObject = new GameObject("ActionSafeAreaController");
            var canvasObject = new GameObject(
                "ActionSafeAreaCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler));
            var railObject = new GameObject("RightRail", typeof(RectTransform));
            var buttonObject = new GameObject(
                "DecorationModeButton",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));
            try
            {
                var camera = cameraObject.AddComponent<UnityEngine.Camera>();
                var controller = controllerObject.AddComponent<DecorationModeController>();
                var canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var rail = railObject.GetComponent<RectTransform>();
                rail.SetParent(canvasObject.transform, false);
                rail.anchorMin = Vector2.one;
                rail.anchorMax = Vector2.one;
                rail.pivot = Vector2.one;
                rail.anchoredPosition = new Vector2(-24f, -24f);
                rail.sizeDelta = new Vector2(180f, 320f);
                var button = buttonObject.GetComponent<Button>();
                button.transform.SetParent(rail, false);
                SetField(controller, "targetCamera", camera);
                SetField(controller, "decorationModeButton", button);
                yield return null;
                Canvas.ForceUpdateCanvases();

                var safeArea = (Rect)InvokePrivate(controller, "GetActionPresentationSafeArea");
                var corners = new Vector3[4];
                rail.GetWorldCorners(corners);
                var railLeft = corners.Min(corner => corner.x);
                Assert.That(safeArea.xMax, Is.LessThanOrEqualTo(railLeft - 16f + 0.01f));
            }
            finally
            {
                UnityEngine.Object.Destroy(cameraObject);
                UnityEngine.Object.Destroy(controllerObject);
                UnityEngine.Object.Destroy(canvasObject);
            }
        }

        [Test]
        public void Controller_ActionSafeAreaExcludesCollapsedHandleAndModalContentWithoutFrameAllocations()
        {
            using var fixture = CreateControllerFixture();
            var railObject = new GameObject("Task7RightRail", typeof(RectTransform));
            var modalContentObject = new GameObject("Content", typeof(RectTransform));
            fixture.Track(railObject);
            fixture.Track(modalContentObject);
            var rail = railObject.GetComponent<RectTransform>();
            rail.SetParent(fixture.CanvasRoot.transform, false);
            rail.anchorMin = Vector2.one;
            rail.anchorMax = Vector2.one;
            rail.pivot = Vector2.one;
            rail.sizeDelta = new Vector2(160f, 360f);
            rail.anchoredPosition = new Vector2(-24f, -24f);
            fixture.HudButton.transform.SetParent(rail, false);
            var handle = fixture.Catalogue.Collapsed.GetComponent<RectTransform>();
            handle.anchorMin = new Vector2(0.5f, 0f);
            handle.anchorMax = new Vector2(0.5f, 0f);
            handle.pivot = new Vector2(0.5f, 0f);
            handle.sizeDelta = new Vector2(240f, 64f);
            handle.anchoredPosition = new Vector2(0f, 24f);

            var modalContent = modalContentObject.GetComponent<RectTransform>();
            modalContent.SetParent(fixture.Modal.Root.transform, false);
            modalContent.anchorMin = Vector2.one * 0.5f;
            modalContent.anchorMax = Vector2.one * 0.5f;
            modalContent.pivot = Vector2.one * 0.5f;
            modalContent.sizeDelta = new Vector2(420f, 240f);
            fixture.Modal.Title.transform.SetParent(modalContent, false);
            fixture.Modal.Body.transform.SetParent(modalContent, false);
            fixture.Modal.Cancel.transform.SetParent(modalContent, false);
            fixture.Modal.Confirm.transform.SetParent(modalContent, false);

            fixture.Controller.EnterDecorationMode();
            fixture.SelectCatalogue(0);
            Canvas.ForceUpdateCanvases();

            var collapsedSafeArea =
                (Rect)InvokePrivate(fixture.Controller, "GetActionPresentationSafeArea");
            var handleRect = GetScreenRect(handle);
            Assert.That(collapsedSafeArea.Overlaps(handleRect), Is.False,
                "The floating action area must exclude the visible Catalogue handle.");

            fixture.Action.Cancel.onClick.Invoke();
            fixture.SelectExisting("00000000000000000000000000000001");
            fixture.Action.Store.onClick.Invoke();
            Canvas.ForceUpdateCanvases();

            var modalSafeArea =
                (Rect)InvokePrivate(fixture.Controller, "GetActionPresentationSafeArea");
            var modalRect = GetScreenRect(modalContent);
            Assert.That(modalSafeArea.Overlaps(modalRect), Is.False,
                "The floating action area must not sit under the Store Modal content.");

            var cachedCorners = GetField<Vector3[]>(
                fixture.Controller, "actionPresentationCorners");
            Assert.That(cachedCorners, Has.Length.EqualTo(4));
            InvokePrivate(fixture.Controller, "GetActionPresentationSafeArea");
            Assert.That(GetField<Vector3[]>(fixture.Controller, "actionPresentationCorners"),
                Is.SameAs(cachedCorners),
                "Action placement must reuse its corner buffer instead of allocating each frame.");

            var safeAreaMethod = typeof(DecorationModeController).GetMethod(
                "GetActionPresentationSafeArea",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(safeAreaMethod, Is.Not.Null);
            var readSafeArea = (Func<Rect>)Delegate.CreateDelegate(
                typeof(Func<Rect>), fixture.Controller, safeAreaMethod);
            for (var index = 0; index < 8; index++) readSafeArea();
            var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            for (var index = 0; index < 64; index++) readSafeArea();
            var allocatedAfter = GC.GetAllocatedBytesForCurrentThread();
            Assert.That(allocatedAfter - allocatedBefore, Is.Zero,
                "Steady-state action placement must not allocate managed memory per frame.");
        }

        [Test]
        public void Controller_StateView_ExistingSuccessHidesFormalAndShowsStore()
        {
            using var fixture = CreateControllerFixture();
            fixture.Controller.EnterDecorationMode();

            fixture.SelectExisting("00000000000000000000000000000001");

            Assert.That(fixture.Controller.State,
                Is.EqualTo(DecorationSessionState.EditingExistingFurniture));
            Assert.That(fixture.Action.View.IsVisible, Is.True);
            Assert.That(fixture.Action.Store.gameObject.activeSelf, Is.True);
            Assert.That(fixture.Registry.TryGet(
                "00000000000000000000000000000001", out var formal), Is.True);
            Assert.That(formal.activeSelf, Is.False);
            Assert.That(fixture.PreviewRoot.childCount, Is.EqualTo(1));
        }

        [Test]
        public void Controller_StateView_ExistingFailureRestoresBrowsingAndPriorFormal()
        {
            using var fixture = CreateControllerFixture();
            fixture.Controller.EnterDecorationMode();
            fixture.SelectExisting("00000000000000000000000000000001");

            fixture.SelectExisting("ffffffffffffffffffffffffffffffff");

            Assert.That(fixture.Controller.State,
                Is.EqualTo(DecorationSessionState.BrowsingCatalogue));
            Assert.That(fixture.Catalogue.View.IsCatalogueVisible, Is.True);
            Assert.That(fixture.Action.View.IsVisible, Is.False);
            Assert.That(fixture.ActivePreviewObjectCount, Is.Zero);
            Assert.That(fixture.Registry.TryGet(
                "00000000000000000000000000000001", out var formal), Is.True);
            Assert.That(formal.activeSelf, Is.True);
        }

        [Test]
        public void Controller_StateView_RotateSuccessKeepsValidPreviewWithoutRebuild()
        {
            using var fixture = CreateControllerFixture();
            fixture.Controller.EnterDecorationMode();
            fixture.SelectExisting("00000000000000000000000000000001");
            var previewObject = fixture.PreviewRoot.GetChild(0).gameObject;

            fixture.Action.Rotate.onClick.Invoke();

            Assert.That(fixture.Controller.State,
                Is.EqualTo(DecorationSessionState.EditingExistingFurniture));
            Assert.That(fixture.Session.ActivePreview.ProposedRotation,
                Is.EqualTo(FurnitureRotation.Degrees90));
            Assert.That(fixture.Session.ActivePreview.PlacementResult.Succeeded, Is.True);
            Assert.That(fixture.PreviewRoot.GetChild(0).gameObject, Is.SameAs(previewObject));
            Assert.That(fixture.Action.Confirm.interactable, Is.True);
        }

        [Test]
        public void Controller_RotateAsymmetricPreviewPreservesVisualCenterAndFullFootprint()
        {
            using var fixture = CreateControllerFixture();
            fixture.SetCameraFloorPoint(
                fixture.GridRoot.TransformPoint(new Vector3(4.5f, 0f, 4.5f)));
            fixture.Controller.EnterDecorationMode();
            fixture.SelectCatalogue(2);
            var previewObject = fixture.PreviewRoot.GetChild(0);
            var visualCenter = previewObject.localPosition;
            Assert.That(fixture.Session.ActivePreview.ProposedPosition,
                Is.EqualTo(new GridPosition(4, 4)));
            Assert.That(fixture.ActiveFootprintCellCount, Is.EqualTo(3));

            fixture.Action.Rotate.onClick.Invoke();

            Assert.That(fixture.Session.ActivePreview.ProposedRotation,
                Is.EqualTo(FurnitureRotation.Degrees90));
            Assert.That(fixture.Session.ActivePreview.ProposedPosition,
                Is.EqualTo(new GridPosition(3, 5)));
            Assert.That(previewObject.localPosition,
                Is.EqualTo(visualCenter).Using(
                    UnityEngine.TestTools.Utils.Vector3ComparerWithEqualsOperator.Instance));
            Assert.That(previewObject.localRotation,
                Is.EqualTo(fixture.GridSpace.GetLocalRotation(FurnitureRotation.Degrees90))
                    .Using(UnityEngine.TestTools.Utils.QuaternionEqualityComparer.Instance));
            Assert.That(fixture.ActiveFootprintCellCount, Is.EqualTo(3));
            Assert.That(fixture.Action.Confirm.interactable, Is.True);
        }

        [Test]
        public void Controller_NewPreviewDragBeyondFloorClampsEntireFootprintAtEdge()
        {
            using var fixture = CreateControllerFixture();
            fixture.Controller.EnterDecorationMode();
            fixture.SelectCatalogue(1);

            fixture.MovePreviewTo(new GridPosition(7, 7));

            Assert.That(fixture.Controller.State,
                Is.EqualTo(DecorationSessionState.PreviewingNewFurniture));
            Assert.That(fixture.Session.ActivePreview.ProposedPosition,
                Is.EqualTo(new GridPosition(7, 6)));
            Assert.That(fixture.Session.ActivePreview.PlacementResult.Succeeded, Is.True);
            Assert.That(fixture.Action.Confirm.interactable, Is.True);
            Assert.That(fixture.ActiveFootprintCellCount, Is.EqualTo(2));
        }

        [Test]
        public void Controller_ExistingPreviewDragBeyondFloorStopsAtNearestEdgeCell()
        {
            using var fixture = CreateControllerFixture();
            var source = fixture.Layout.FurnitureInstances.Single();
            fixture.Controller.EnterDecorationMode();
            fixture.SelectExisting(source.InstanceId);

            fixture.MovePreviewTo(new GridPosition(8, 8));

            Assert.That(fixture.Controller.State,
                Is.EqualTo(DecorationSessionState.EditingExistingFurniture));
            Assert.That(fixture.Session.ActivePreview.SourceInstanceId,
                Is.EqualTo(source.InstanceId));
            Assert.That(fixture.Session.ActivePreview.ProposedPosition,
                Is.EqualTo(new GridPosition(7, 7)));
            Assert.That(fixture.Session.ActivePreview.PlacementResult.Succeeded, Is.True);
            Assert.That(fixture.Layout.TryGetFurnitureInstance(source.InstanceId, out var present),
                Is.True);
            Assert.That(present, Is.SameAs(source));
            Assert.That(fixture.Registry.TryGet(source.InstanceId, out var hiddenFormal), Is.True);
            Assert.That(hiddenFormal.activeSelf, Is.False);
            Assert.That(fixture.Action.Store.gameObject.activeSelf, Is.True);
            Assert.That(fixture.Action.Confirm.interactable, Is.True);
            Assert.That(fixture.Action.StateShape.activeSelf, Is.False);
            Assert.That(fixture.PreviewRoot.childCount, Is.EqualTo(1));
            Assert.That(fixture.ActiveFootprintCellCount, Is.EqualTo(1));
        }

        [Test]
        public void Controller_ActionPresentationUsesEightPixelFurnitureGap()
        {
            using var fixture = CreateControllerFixture();
            fixture.Controller.EnterDecorationMode();
            fixture.SelectCatalogue(0);
            Assert.That(fixture.Preview.TryGetWorldBounds(out var bounds), Is.True);

            var preferred = (Vector2)InvokePrivate(
                fixture.Controller,
                "GetActionPresentationPreferredPoint",
                bounds);
            var maximum = new Vector2(float.MinValue, float.MinValue);
            for (var index = 0; index < 8; index++)
            {
                var corner = new Vector3(
                    (index & 1) == 0 ? bounds.min.x : bounds.max.x,
                    (index & 2) == 0 ? bounds.min.y : bounds.max.y,
                    (index & 4) == 0 ? bounds.min.z : bounds.max.z);
                var screen = fixture.Camera.WorldToScreenPoint(corner);
                if (screen.z >= 0f)
                {
                    maximum.x = Mathf.Max(maximum.x, screen.x);
                    maximum.y = Mathf.Max(maximum.y, screen.y);
                }
            }

            Assert.That(preferred, Is.EqualTo(maximum + new Vector2(8f, 8f))
                .Using(UnityEngine.TestTools.Utils.Vector2ComparerWithEqualsOperator.Instance));
        }

        [Test]
        public void Controller_StateView_ConfirmSuccessRebuildsFormalAndReturnsBrowsing()
        {
            using var fixture = CreateControllerFixture();
            fixture.Controller.EnterDecorationMode();
            fixture.SelectCatalogue(0);
            var beforeCount = fixture.Layout.FurnitureInstances.Count;

            fixture.Action.Confirm.onClick.Invoke();

            Assert.That(fixture.Layout.FurnitureInstances, Has.Count.EqualTo(beforeCount + 1));
            Assert.That(fixture.Controller.State,
                Is.EqualTo(DecorationSessionState.BrowsingCatalogue));
            Assert.That(fixture.ActivePreviewObjectCount, Is.Zero);
            Assert.That(fixture.Catalogue.View.IsCatalogueVisible, Is.True);
            Assert.That(fixture.Catalogue.View.IsCollapsed, Is.True,
                "Confirm keeps the catalogue compact so the committed furniture can be selected again.");
            foreach (var instance in fixture.Layout.FurnitureInstances)
            {
                Assert.That(fixture.Registry.TryGet(instance.InstanceId, out _), Is.True);
            }
        }

        [Test]
        public void Controller_RapidRepeatedDefinitionConfirmsKeepUniqueStableRegistryEntries()
        {
            using var fixture = CreateControllerFixture();
            fixture.Controller.EnterDecorationMode();
            var placements = new[]
            {
                new GridPosition(0, 0),
                new GridPosition(1, 0),
                new GridPosition(0, 1),
                new GridPosition(1, 1)
            };

            for (var index = 0; index < placements.Length; index++)
            {
                var placement = placements[index];
                fixture.Catalogue.View.ShowCatalogue();
                fixture.SelectCatalogue(0);
                fixture.MovePreviewTo(placement);
                Assert.That(fixture.Session.ActivePreview, Is.Not.Null,
                    "Rapid confirm preview missing at iteration " + index + ".");
                Assert.That(fixture.Session.ActivePreview.PlacementResult.Succeeded, Is.True);
                fixture.Action.Confirm.onClick.Invoke();
            }

            var moduleInstances = fixture.Layout.FurnitureInstances
                .Where(instance => instance.DefinitionId == "furniture.counter.module.01")
                .ToArray();
            var ids = moduleInstances.Select(instance => instance.InstanceId).ToArray();
            Assert.That(moduleInstances, Has.Length.EqualTo(placements.Length + 1));
            Assert.That(ids.Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(ids.Length));
            Assert.That(ids, Does.Contain("00000000000000000000000000000001"));
            foreach (var instance in moduleInstances)
            {
                Assert.That(fixture.Registry.TryGet(instance.InstanceId, out var formal), Is.True);
                Assert.That(formal.activeInHierarchy, Is.True);
            }

            Assert.That(fixture.FormalRoot.Cast<Transform>()
                .Count(child => child.gameObject.activeSelf), Is.EqualTo(moduleInstances.Length));
        }

        [Test]
        public void Controller_ExistingConfirmRestoresVisibleFormalAtCommittedPlacement()
        {
            using var fixture = CreateControllerFixture();
            fixture.Controller.EnterDecorationMode();
            fixture.SelectExisting("00000000000000000000000000000001");
            Assert.That(fixture.Registry.TryGet(
                "00000000000000000000000000000001", out var hiddenFormal), Is.True);
            Assert.That(hiddenFormal.activeSelf, Is.False);
            fixture.MovePreviewTo(new GridPosition(5, 5));
            fixture.Action.Rotate.onClick.Invoke();

            fixture.Action.Confirm.onClick.Invoke();

            Assert.That(fixture.Layout.TryGetFurnitureInstance(
                "00000000000000000000000000000001", out var committed), Is.True);
            Assert.That(committed.Position, Is.EqualTo(new GridPosition(5, 5)));
            Assert.That(committed.Rotation, Is.EqualTo(FurnitureRotation.Degrees90));
            Assert.That(fixture.Registry.TryGet(
                committed.InstanceId, out var rebuiltFormal), Is.True);
            Assert.That(rebuiltFormal.activeInHierarchy, Is.True);
            Assert.That(rebuiltFormal.transform.localPosition,
                Is.EqualTo(fixture.GridSpace.GetFootprintCenterLocal(
                    new[] { new GridPosition(5, 5) })).Using(
                    UnityEngine.TestTools.Utils.Vector3ComparerWithEqualsOperator.Instance));
            Assert.That(rebuiltFormal.transform.localRotation,
                Is.EqualTo(fixture.GridSpace.GetLocalRotation(FurnitureRotation.Degrees90))
                    .Using(UnityEngine.TestTools.Utils.QuaternionEqualityComparer.Instance));
            Assert.That(fixture.Session.ActivePreview, Is.Null);
            Assert.That(fixture.Controller.State,
                Is.EqualTo(DecorationSessionState.BrowsingCatalogue));
        }

        [Test]
        public void Controller_NewConfirm_AllowsCommittedFurnitureToBeSelectedAgain()
        {
            using var fixture = CreateControllerFixture();
            fixture.Controller.EnterDecorationMode();
            fixture.SelectCatalogue(0);
            var committedPosition = fixture.Session.ActivePreview.ProposedPosition;

            fixture.Action.Confirm.onClick.Invoke();

            var committed = fixture.Layout.FurnitureInstances
                .Single(instance => instance.Position == committedPosition
                    && instance.InstanceId != "00000000000000000000000000000001");
            var hit = fixture.Classify(fixture.ScreenForCell(committedPosition));
            Assert.That(hit.Kind, Is.EqualTo(DecorationTouchHitKind.Furniture));
            Assert.That(hit.FurnitureInstanceId, Is.EqualTo(committed.InstanceId));

            fixture.SelectExisting(hit.FurnitureInstanceId);

            Assert.That(fixture.Controller.State,
                Is.EqualTo(DecorationSessionState.EditingExistingFurniture));
            Assert.That(fixture.Session.ActivePreview.SourceInstanceId,
                Is.EqualTo(committed.InstanceId));
            Assert.That(fixture.Action.View.IsVisible, Is.True);
            AssertCatalogueCollapsedForPreview(fixture);
        }

        [Test]
        public void Controller_StateView_ConfirmFailureKeepsRetryWindowAndNoRebuild()
        {
            using var fixture = CreateControllerFixture();
            fixture.Controller.EnterDecorationMode();
            fixture.SelectExisting("00000000000000000000000000000001");
            Assert.That(fixture.Layout.RemoveFurniture(
                "00000000000000000000000000000001").Succeeded, Is.True);

            fixture.Action.Confirm.onClick.Invoke();

            Assert.That(fixture.Controller.State,
                Is.EqualTo(DecorationSessionState.EditingExistingFurniture));
            Assert.That(fixture.Session.ActivePreview.PlacementResult.FailureReason,
                Is.EqualTo(PlacementFailureReason.InstanceNotFound));
            Assert.That(fixture.Action.View.IsVisible, Is.True);
            Assert.That(fixture.Action.Store.gameObject.activeSelf, Is.False);
            Assert.That(fixture.Action.Confirm.interactable, Is.False);
            Assert.That(fixture.PreviewRoot.childCount, Is.EqualTo(1));
        }

        [Test]
        public void Controller_StateView_CancelRestoresAuthoritativeRepresentation()
        {
            using var fixture = CreateControllerFixture();
            var original = fixture.Layout.FurnitureInstances.Single();
            var occupiedBefore = fixture.Layout.OccupiedCellCount;
            fixture.Controller.EnterDecorationMode();
            fixture.SelectExisting("00000000000000000000000000000001");
            fixture.MovePreviewTo(new GridPosition(5, 5));
            fixture.Action.Rotate.onClick.Invoke();
            Assert.That(fixture.Session.ActivePreview.ProposedRotation,
                Is.EqualTo(FurnitureRotation.Degrees90));

            fixture.Action.Cancel.onClick.Invoke();

            Assert.That(fixture.Controller.State,
                Is.EqualTo(DecorationSessionState.BrowsingCatalogue));
            Assert.That(fixture.Session.ActivePreview, Is.Null);
            Assert.That(fixture.Registry.TryGet(
                "00000000000000000000000000000001", out var formal), Is.True);
            Assert.That(formal.activeSelf, Is.True);
            Assert.That(fixture.Layout.TryGetFurnitureInstance(original.InstanceId, out var restored),
                Is.True);
            Assert.That(restored, Is.SameAs(original));
            Assert.That(restored.Position, Is.EqualTo(new GridPosition(2, 3)));
            Assert.That(restored.Rotation, Is.EqualTo(FurnitureRotation.Degrees0));
            Assert.That(fixture.Layout.OccupiedCellCount, Is.EqualTo(occupiedBefore));
            Assert.That(fixture.Layout.TryGetOccupant(
                new GridPosition(2, 3), out var restoredOccupant), Is.True);
            Assert.That(restoredOccupant, Is.EqualTo(original.InstanceId));
            Assert.That(fixture.Layout.TryGetOccupant(
                new GridPosition(5, 5), out _), Is.False);
            Assert.That(formal.transform.localPosition,
                Is.EqualTo(fixture.GridSpace.GetCellCenterLocal(new GridPosition(2, 3)))
                    .Using(UnityEngine.TestTools.Utils.Vector3ComparerWithEqualsOperator.Instance));
            Assert.That(formal.transform.localRotation,
                Is.EqualTo(fixture.GridSpace.GetLocalRotation(FurnitureRotation.Degrees0))
                    .Using(UnityEngine.TestTools.Utils.QuaternionEqualityComparer.Instance));
            Assert.That(fixture.ActivePreviewObjectCount, Is.Zero);
        }

        [Test]
        public void Controller_NewCancelDoesNotMutateFormalLayoutOrRegistry()
        {
            using var fixture = CreateControllerFixture();
            var initial = fixture.Layout.FurnitureInstances.Single();
            Assert.That(fixture.Registry.TryGet(initial.InstanceId, out var initialFormal), Is.True);
            var occupiedBefore = fixture.Layout.OccupiedCellCount;
            fixture.Controller.EnterDecorationMode();
            fixture.SelectCatalogue(3);
            fixture.MovePreviewTo(new GridPosition(4, 4));

            fixture.Action.Cancel.onClick.Invoke();

            Assert.That(fixture.Layout.FurnitureInstances, Has.Count.EqualTo(1));
            Assert.That(fixture.Layout.FurnitureInstances[0], Is.SameAs(initial));
            Assert.That(fixture.Layout.OccupiedCellCount, Is.EqualTo(occupiedBefore));
            Assert.That(fixture.Registry.TryGet(initial.InstanceId, out var restoredFormal), Is.True);
            Assert.That(restoredFormal, Is.SameAs(initialFormal));
            Assert.That(restoredFormal.activeInHierarchy, Is.True);
            Assert.That(fixture.Session.ActivePreview, Is.Null);
            Assert.That(fixture.Controller.State,
                Is.EqualTo(DecorationSessionState.BrowsingCatalogue));
        }

        [Test]
        public void Controller_StateView_StoreRequestRetainsPreviewAndOpensModal()
        {
            using var fixture = CreateControllerFixture();
            fixture.Controller.EnterDecorationMode();
            fixture.SelectExisting("00000000000000000000000000000001");
            var previewObject = fixture.PreviewRoot.GetChild(0).gameObject;

            fixture.Action.Store.onClick.Invoke();

            Assert.That(fixture.Controller.State,
                Is.EqualTo(DecorationSessionState.ConfirmingStore));
            Assert.That(fixture.Modal.View.IsOpen, Is.True);
            Assert.That(fixture.Action.View.IsVisible, Is.True);
            Assert.That(fixture.PreviewRoot.GetChild(0).gameObject, Is.SameAs(previewObject));
            Assert.That(fixture.ActiveFootprintCellCount, Is.EqualTo(1));
        }

        [Test]
        public void Controller_StateView_ModalDismissRestoresExistingActionWindow()
        {
            using var fixture = CreateControllerFixture();
            fixture.Controller.EnterDecorationMode();
            fixture.SelectExisting("00000000000000000000000000000001");
            fixture.Action.Store.onClick.Invoke();

            fixture.Modal.Cancel.onClick.Invoke();

            Assert.That(fixture.Controller.State,
                Is.EqualTo(DecorationSessionState.EditingExistingFurniture));
            Assert.That(fixture.Modal.View.IsOpen, Is.False);
            Assert.That(fixture.Action.View.IsVisible, Is.True);
            Assert.That(fixture.Action.Store.gameObject.activeSelf, Is.True);
            Assert.That(fixture.PreviewRoot.childCount, Is.EqualTo(1));
        }

        [Test]
        public void Controller_StateView_StoreSuccessRemovesOnlySourceAndReturnsBrowsing()
        {
            using var fixture = CreateControllerFixture(addSecondFurniture: true);
            Assert.That(fixture.Layout.TryGetFurnitureInstance(
                "00000000000000000000000000000002", out var secondBefore), Is.True);
            fixture.Controller.EnterDecorationMode();
            fixture.SelectExisting("00000000000000000000000000000001");
            fixture.Action.Store.onClick.Invoke();

            fixture.Modal.Confirm.onClick.Invoke();
            Assert.That(fixture.Layout.TryGetFurnitureInstance(
                secondBefore.InstanceId, out var secondAfterFirstConfirm), Is.True);
            Assert.That(fixture.Registry.TryGet(
                secondBefore.InstanceId, out var secondFormalAfterFirstConfirm), Is.True);
            var secondPositionAfterFirstConfirm = secondAfterFirstConfirm.Position;
            var secondRotationAfterFirstConfirm = secondAfterFirstConfirm.Rotation;
            var secondFormalPositionAfterFirstConfirm =
                secondFormalAfterFirstConfirm.transform.localPosition;
            var secondFormalRotationAfterFirstConfirm =
                secondFormalAfterFirstConfirm.transform.localRotation;
            fixture.Modal.Confirm.onClick.Invoke();

            Assert.That(fixture.Controller.State,
                Is.EqualTo(DecorationSessionState.BrowsingCatalogue));
            Assert.That(fixture.Layout.TryGetFurnitureInstance(
                "00000000000000000000000000000001", out _), Is.False);
            Assert.That(fixture.Layout.TryGetFurnitureInstance(
                secondBefore.InstanceId, out var secondAfter), Is.True);
            Assert.That(secondAfter, Is.SameAs(secondAfterFirstConfirm));
            Assert.That(secondAfter, Is.SameAs(secondBefore));
            Assert.That(secondAfter.Position, Is.EqualTo(secondPositionAfterFirstConfirm));
            Assert.That(secondAfter.Rotation, Is.EqualTo(secondRotationAfterFirstConfirm));
            Assert.That(fixture.Layout.TryGetOccupant(
                secondBefore.Position, out var secondOccupant), Is.True);
            Assert.That(secondOccupant, Is.EqualTo(secondBefore.InstanceId));
            Assert.That(fixture.Registry.TryGet(
                "00000000000000000000000000000001", out _), Is.False);
            Assert.That(fixture.Registry.TryGet(
                secondBefore.InstanceId, out var secondFormalAfter), Is.True);
            Assert.That(secondFormalAfter, Is.SameAs(secondFormalAfterFirstConfirm));
            Assert.That(secondFormalAfter.activeInHierarchy, Is.True);
            Assert.That(secondFormalAfter.transform.localPosition,
                Is.EqualTo(secondFormalPositionAfterFirstConfirm).Using(
                    UnityEngine.TestTools.Utils.Vector3ComparerWithEqualsOperator.Instance));
            Assert.That(secondFormalAfter.transform.localRotation,
                Is.EqualTo(secondFormalRotationAfterFirstConfirm).Using(
                    UnityEngine.TestTools.Utils.QuaternionEqualityComparer.Instance));
            Assert.That(fixture.Catalogue.View.IsCatalogueVisible, Is.True);
        }

        [Test]
        public void Controller_ConsecutiveStoreConfirmationsAcceptASecondInstanceWindow()
        {
            using var fixture = CreateControllerFixture(addSecondFurniture: true);
            fixture.Controller.EnterDecorationMode();

            fixture.SelectExisting("00000000000000000000000000000001");
            fixture.Action.Store.onClick.Invoke();
            fixture.Modal.Confirm.onClick.Invoke();
            Assert.That(fixture.Layout.TryGetFurnitureInstance(
                "00000000000000000000000000000001", out _), Is.False);

            fixture.SelectExisting("00000000000000000000000000000002");
            fixture.Action.Store.onClick.Invoke();
            Assert.That(fixture.Modal.View.IsOpen, Is.True,
                "A fresh Store window must reset the Modal terminal-action latch.");
            fixture.Modal.Confirm.onClick.Invoke();

            Assert.That(fixture.Controller.State,
                Is.EqualTo(DecorationSessionState.BrowsingCatalogue));
            Assert.That(fixture.Layout.FurnitureInstances, Is.Empty);
            Assert.That(fixture.Registry.TryGet(
                "00000000000000000000000000000001", out _), Is.False);
            Assert.That(fixture.Registry.TryGet(
                "00000000000000000000000000000002", out _), Is.False);
            Assert.That(fixture.FormalRoot.Cast<Transform>()
                .Count(child => child.gameObject.activeSelf), Is.Zero);
            Assert.That(fixture.Modal.View.IsOpen, Is.False);
            Assert.That(fixture.Catalogue.View.IsCatalogueVisible, Is.True);
        }

        [Test]
        public void Controller_StateView_StoreFailureDismissesToInvalidExistingPreview()
        {
            using var fixture = CreateControllerFixture();
            fixture.Controller.EnterDecorationMode();
            fixture.SelectExisting("00000000000000000000000000000001");
            fixture.Action.Store.onClick.Invoke();
            Assert.That(fixture.Layout.RemoveFurniture(
                "00000000000000000000000000000001").Succeeded, Is.True);

            fixture.Modal.Confirm.onClick.Invoke();

            Assert.That(fixture.Controller.State,
                Is.EqualTo(DecorationSessionState.EditingExistingFurniture));
            Assert.That(fixture.Modal.View.IsOpen, Is.False);
            Assert.That(fixture.Action.Store.gameObject.activeSelf, Is.False);
            Assert.That(fixture.Action.Confirm.interactable, Is.False);
            Assert.That(fixture.PreviewRoot.childCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator Controller_StateView_ExitClosesEveryOwnedPresentation()
        {
            using var fixture = CreateControllerFixture();
            fixture.Controller.EnterDecorationMode();
            fixture.SelectExisting("00000000000000000000000000000001");

            fixture.Controller.ExitDecorationMode();

            Assert.That(fixture.Controller.State, Is.EqualTo(DecorationSessionState.Closed));
            Assert.That(fixture.Controller.IsOpen, Is.False);
            Assert.That(fixture.Catalogue.View.IsCatalogueVisible, Is.False);
            Assert.That(fixture.Action.View.IsVisible, Is.False);
            Assert.That(fixture.Modal.View.IsOpen, Is.False);
            Assert.That(fixture.ActivePreviewObjectCount, Is.Zero);
            Assert.That(fixture.ActiveGridCellCount, Is.Zero);
            yield return null;
            Assert.That(fixture.PreviewRoot.childCount, Is.Zero,
                "Cleanup must destroy and detach the Preview object, not merely hide it.");
        }

        [Test]
        public void Controller_EnterIsIdempotentAndAcquiresOnePauseHandle()
        {
            using var fixture = CreateControllerFixture();

            fixture.Controller.EnterDecorationMode();
            fixture.Controller.EnterDecorationMode();

            Assert.That(fixture.Time.SetRequests, Is.EqualTo(1));
            Assert.That(fixture.Time.CurrentSpeed, Is.EqualTo(GameSpeed.Paused));
            Assert.That(fixture.Controller.IsOpen, Is.True);
        }

        [Test]
        public void Controller_DisabledOwnerCannotEnterDecorationMode()
        {
            using var fixture = CreateControllerFixture();
            fixture.Controller.enabled = false;

            fixture.Controller.EnterDecorationMode();

            Assert.That(fixture.Controller.IsOpen, Is.False);
            Assert.That(fixture.Controller.State, Is.EqualTo(DecorationSessionState.Closed));
            Assert.That(fixture.Time.CurrentSpeed, Is.EqualTo(GameSpeed.Normal));
            Assert.That(fixture.CameraController.enabled, Is.True);
        }

        [Test]
        public void Controller_PauseAcquireRejectedLeavesClosedAndClean()
        {
            using var fixture = CreateControllerFixture();
            fixture.Time.RejectNextRequest = true;

            Assert.Throws<InvalidOperationException>(() => fixture.Controller.EnterDecorationMode());

            fixture.AssertClosedAndClean();
            Assert.That(fixture.CameraController.enabled, Is.True);
            Assert.That(fixture.SceneInteraction.TrySelectAt(
                fixture.ScreenForCell(new GridPosition(2, 3))), Is.True);
        }

        [UnityTest]
        public IEnumerator Controller_PostAcquireFailureRollsBackPauseInputAndCamera()
        {
            using var fixture = CreateControllerFixture();
            fixture.Registry.Rebuild(fixture.Layout.FurnitureInstances);
            Assert.That(fixture.Registry.TryGet(
                "00000000000000000000000000000001", out var formal), Is.True);
            var selectable = formal.GetComponentInChildren<Task4Selectable>();
            var screen = fixture.Camera.WorldToScreenPoint(selectable.transform.position);
            fixture.BreakCatalogueReferences();

            Assert.Throws<InvalidOperationException>(() => fixture.Controller.EnterDecorationMode());

            fixture.AssertClosedAndClean();
            Assert.That(fixture.Time.CurrentSpeed, Is.EqualTo(GameSpeed.Normal));
            Assert.That(fixture.CameraController.enabled, Is.True);

            fixture.LegacyInput.NextFrame = new AnimalCafe.Input.CameraInputFrame(
                Vector2.zero,
                0f,
                false,
                screen,
                -1,
                pointerPressed: true);
            yield return null;
            fixture.LegacyInput.NextFrame = new AnimalCafe.Input.CameraInputFrame(
                Vector2.zero,
                0f,
                true,
                screen,
                -1,
                pointerReleased: true);
            yield return null;

            Assert.That(fixture.SceneInteraction.CurrentSelection, Is.SameAs(selectable),
                "Rollback must release its suppression lease so a later fresh press can select.");
        }

        [Test]
        public void Controller_FailedEnterCanRetrySuccessfully()
        {
            using var fixture = CreateControllerFixture();
            fixture.BreakCatalogueReferences();
            Assert.Throws<InvalidOperationException>(() => fixture.Controller.EnterDecorationMode());
            fixture.RestoreCatalogueReferences();

            Assert.DoesNotThrow(() => fixture.Controller.EnterDecorationMode());

            Assert.That(fixture.Controller.IsOpen, Is.True);
            Assert.That(fixture.Controller.State,
                Is.EqualTo(DecorationSessionState.BrowsingCatalogue));
            Assert.That(fixture.Time.CurrentSpeed, Is.EqualTo(GameSpeed.Paused));
        }

        [Test]
        public void Controller_NestedPauseUsesOneSharedCoordinator()
        {
            using var fixture = CreateControllerFixture();
            fixture.Controller.EnterDecorationMode();
            var shared = GetField<UiPauseCoordinator>(fixture.Controller, "pauseCoordinator");
            var otherView = new UiView(
                "task7.other-pause",
                UiViewKind.Modal,
                UiPausePolicy.PauseGame,
                UiOutsideDismissPolicy.NotDismissible);
            using var other = shared.Acquire(otherView);

            fixture.Controller.ExitDecorationMode();

            Assert.That(fixture.Time.CurrentSpeed, Is.EqualTo(GameSpeed.Paused));
            other.Dispose();
            Assert.That(fixture.Time.CurrentSpeed, Is.EqualTo(GameSpeed.Normal));
        }

        [Test]
        public void Controller_ConfiguresTask6ViewsWithSameSharedServices()
        {
            using var fixture = CreateControllerFixture();

            fixture.Controller.EnterDecorationMode();

            var pause = GetField<UiPauseCoordinator>(fixture.Controller, "pauseCoordinator");
            var pointer = GetField<UiPointerBoundary>(fixture.Controller, "pointerBoundary");
            var navigation = GetField<UiNavigationCoordinator>(
                fixture.Controller, "navigationCoordinator");
            var transition = GetField<UiTransitionRunner>(fixture.Controller, "transitionRunner");
            Assert.That(GetField<IUiPointerOwnershipRegistrar>(
                fixture.Catalogue.View, "pointerBoundary"), Is.SameAs(pointer));
            Assert.That(GetField<UiTransitionRunner>(
                fixture.Catalogue.View, "transitionRunner"), Is.SameAs(transition));
            Assert.That(GetField<IUiPointerOwnershipRegistrar>(
                fixture.Action.PointerHook, "pointerBoundary"), Is.SameAs(pointer));
            Assert.That(GetField<UiTransitionRunner>(
                fixture.Action.View, "transitionRunner"), Is.SameAs(transition));
            Assert.That(GetField<UiNavigationCoordinator>(
                fixture.Modal.View, "navigation"), Is.SameAs(navigation));
            Assert.That(GetField<UiNavigationCoordinator>(
                fixture.Modal.SharedModal, "navigation"), Is.SameAs(navigation));
            Assert.That(GetField<UiPauseCoordinator>(
                fixture.Modal.SharedModal, "pauseCoordinator"), Is.SameAs(pause));
            Assert.That(GetField<UiPointerBoundary>(
                fixture.Modal.SharedModal, "pointerBoundary"), Is.SameAs(pointer));
            Assert.That(GetField<UiTransitionRunner>(
                fixture.Modal.SharedModal, "transitionRunner"), Is.SameAs(transition));
        }

        [Test]
        public void Controller_ConfirmedLayoutSurvivesExitAndReenterSameRun()
        {
            using var fixture = CreateControllerFixture();
            fixture.Controller.EnterDecorationMode();
            fixture.SelectCatalogue(0);
            fixture.Action.Confirm.onClick.Invoke();
            var confirmedId = fixture.Layout.FurnitureInstances
                .Single(instance => instance.InstanceId != "00000000000000000000000000000001")
                .InstanceId;

            fixture.Controller.ExitDecorationMode();
            fixture.Controller.EnterDecorationMode();

            Assert.That(fixture.Layout.TryGetFurnitureInstance(confirmedId, out _), Is.True);
            Assert.That(fixture.Registry.TryGet(confirmedId, out _), Is.True);
        }

        [Test]
        public void Controller_ExitDisableDestroyCleanupIsIdempotent()
        {
            using var fixture = CreateControllerFixture();
            fixture.Controller.EnterDecorationMode();
            fixture.SelectExisting("00000000000000000000000000000001");

            fixture.Controller.ExitDecorationMode();
            var catalogueTransition = GetField<Coroutine>(
                fixture.Catalogue.View, "transitionCoroutine");
            var actionTransition = GetField<Coroutine>(
                fixture.Action.View, "transitionCoroutine");
            Assert.That(catalogueTransition, Is.Not.Null);
            Assert.That(actionTransition, Is.Not.Null);

            fixture.Controller.ExitDecorationMode();
            Assert.That(GetField<Coroutine>(fixture.Catalogue.View, "transitionCoroutine"),
                Is.SameAs(catalogueTransition));
            Assert.That(GetField<Coroutine>(fixture.Action.View, "transitionCoroutine"),
                Is.SameAs(actionTransition));
            fixture.Controller.enabled = false;
            Assert.That(GetField<Coroutine>(fixture.Catalogue.View, "transitionCoroutine"),
                Is.SameAs(catalogueTransition));
            Assert.That(GetField<Coroutine>(fixture.Action.View, "transitionCoroutine"),
                Is.SameAs(actionTransition));
            fixture.Controller.enabled = true;
            UnityEngine.Object.DestroyImmediate(fixture.Controller);

            Assert.That(GetField<Coroutine>(fixture.Catalogue.View, "transitionCoroutine"),
                Is.SameAs(catalogueTransition));
            Assert.That(GetField<Coroutine>(fixture.Action.View, "transitionCoroutine"),
                Is.SameAs(actionTransition));

            Assert.That(fixture.Time.CurrentSpeed, Is.EqualTo(GameSpeed.Normal));
            Assert.That(fixture.CameraController.enabled, Is.True);
            Assert.That(fixture.ActivePreviewObjectCount, Is.Zero);
            Assert.That(fixture.ActiveGridCellCount, Is.Zero);
        }

        [Test]
        public void Controller_DecorationModeLocksTimeControlsAndEveryCleanupUnlocksThem()
        {
            using var fixture = CreateControllerFixture();

            Assert.That(fixture.NormalTimeButton.interactable, Is.True);
            Assert.That(fixture.FastTimeButton.interactable, Is.True);

            fixture.Controller.EnterDecorationMode();
            Assert.That(fixture.NormalTimeButton.interactable, Is.False);
            Assert.That(fixture.FastTimeButton.interactable, Is.False);

            fixture.Controller.ExitDecorationMode();
            Assert.That(fixture.NormalTimeButton.interactable, Is.True);
            Assert.That(fixture.FastTimeButton.interactable, Is.True);

            fixture.Controller.EnterDecorationMode();
            fixture.Controller.enabled = false;
            Assert.That(fixture.NormalTimeButton.interactable, Is.True);
            Assert.That(fixture.FastTimeButton.interactable, Is.True);
        }

        [UnityTest]
        [TestCase("Exit", false, GameSpeed.Fast, ExpectedResult = null)]
        [TestCase("Disable", false, GameSpeed.Fast, ExpectedResult = null)]
        [TestCase("Destroy", true, GameSpeed.Fast, ExpectedResult = null)]
        public IEnumerator Controller_ActiveLifecycleTriggerRestoresEveryOwnedResource(
            string trigger,
            bool openStoreModal,
            GameSpeed initialSpeed)
        {
            using var fixture = CreateControllerFixture(initialSpeed: initialSpeed);
            Assert.That(fixture.Time.CurrentSpeed, Is.EqualTo(initialSpeed));
            fixture.Controller.EnterDecorationMode();
            fixture.SelectExisting("00000000000000000000000000000001");
            if (openStoreModal)
            {
                fixture.Action.Store.onClick.Invoke();
                Assert.That(fixture.Controller.State,
                    Is.EqualTo(DecorationSessionState.ConfirmingStore));
            }
            else
            {
                BeginFurnitureEdgePan(fixture, 80);
                Assert.That(fixture.CameraDriver.IsEdgeAutoPanning, Is.True);
            }

            var session = fixture.Session;
            Assert.That(fixture.Registry.TryGet(
                "00000000000000000000000000000001", out var hiddenFormal), Is.True);
            Assert.That(hiddenFormal.activeSelf, Is.False);
            Assert.That(fixture.Time.CurrentSpeed, Is.EqualTo(GameSpeed.Paused));
            Assert.That(fixture.CameraController.enabled, Is.False);

            switch (trigger)
            {
                case "Exit":
                    fixture.Controller.ExitDecorationMode();
                    break;
                case "Disable":
                    fixture.Controller.enabled = false;
                    break;
                case "Destroy":
                    UnityEngine.Object.DestroyImmediate(fixture.Controller);
                    break;
                default:
                    Assert.Fail("Unknown lifecycle trigger: " + trigger);
                    break;
            }

            Assert.That(session.State, Is.EqualTo(DecorationSessionState.Closed));
            Assert.That(fixture.Time.CurrentSpeed, Is.EqualTo(initialSpeed));
            Assert.That(fixture.CameraController.enabled, Is.True);
            Assert.That(fixture.Catalogue.View.IsCatalogueVisible, Is.False);
            Assert.That(fixture.Action.View.IsVisible, Is.False);
            Assert.That(fixture.Modal.View.IsOpen, Is.False);
            Assert.That(fixture.ActivePreviewObjectCount, Is.Zero);
            Assert.That(fixture.ActiveGridCellCount, Is.Zero);
            Assert.That(fixture.CameraDriver.IsEdgeAutoPanning, Is.False);
            Assert.That(fixture.Registry.TryGet(
                "00000000000000000000000000000001", out var restoredFormal), Is.True);
            Assert.That(restoredFormal.activeInHierarchy, Is.True);

            yield return null;
            Assert.That(fixture.PreviewRoot.childCount, Is.Zero,
                "Every lifecycle trigger must destroy the owned Preview object.");

            if (!string.Equals(trigger, "Destroy", StringComparison.Ordinal))
            {
                if (string.Equals(trigger, "Disable", StringComparison.Ordinal))
                {
                    fixture.Controller.enabled = true;
                }

                fixture.Controller.EnterDecorationMode();
                var freshScreen = fixture.ScreenForCell(new GridPosition(2, 3));
                fixture.SendTouch(81, freshScreen, Vector2.zero,
                    UnityEngine.InputSystem.TouchPhase.Began);
                Assert.That(fixture.Controller.State,
                    Is.EqualTo(DecorationSessionState.EditingExistingFurniture),
                    "Cleanup must reset the unterminated prior Furniture touch owner.");
                fixture.SendTouch(81, freshScreen, Vector2.zero,
                    UnityEngine.InputSystem.TouchPhase.Ended);
                fixture.Controller.ExitDecorationMode();
            }

            var selectable = restoredFormal.GetComponentInChildren<Task4Selectable>();
            var screen = fixture.Camera.WorldToScreenPoint(selectable.transform.position);
            fixture.LegacyInput.NextFrame = new AnimalCafe.Input.CameraInputFrame(
                Vector2.zero,
                0f,
                false,
                screen,
                -1,
                pointerPressed: true);
            yield return null;
            fixture.LegacyInput.NextFrame = new AnimalCafe.Input.CameraInputFrame(
                Vector2.zero,
                0f,
                true,
                screen,
                -1,
                pointerReleased: true);
            yield return null;
            Assert.That(fixture.SceneInteraction.CurrentSelection, Is.SameAs(selectable),
                "Cleanup must release input suppression after a later fresh press.");
        }

        [Test]
        public void Controller_PendingSpeedRestoreRetriesOnlyWhenClosed()
        {
            using var fixture = CreateControllerFixture();
            fixture.Controller.EnterDecorationMode();
            fixture.Time.RejectRequestsRemaining = 2;

            fixture.Controller.ExitDecorationMode();
            Assert.That(fixture.Time.CurrentSpeed, Is.EqualTo(GameSpeed.Paused));
            var requestsAfterFailedRestore = fixture.Time.SetRequests;

            fixture.InvokeControllerUpdate();
            Assert.That(fixture.Time.SetRequests, Is.EqualTo(requestsAfterFailedRestore + 1));
            Assert.That(fixture.Time.CurrentSpeed, Is.EqualTo(GameSpeed.Normal));
        }

        [Test]
        public void Controller_FinalPauseReleaseImmediatelyRetriesPendingSpeed()
        {
            using var fixture = CreateControllerFixture();
            fixture.Controller.EnterDecorationMode();
            fixture.Time.RejectNextRequest = true;
            var requestsBeforeExit = fixture.Time.SetRequests;

            fixture.Controller.ExitDecorationMode();

            Assert.That(fixture.Time.SetRequests, Is.EqualTo(requestsBeforeExit + 2),
                "Final handle disposal and immediate TryRestorePendingSpeed are distinct attempts.");
            Assert.That(fixture.Time.CurrentSpeed, Is.EqualTo(GameSpeed.Normal));
            Assert.That(GetField<UiPauseCoordinator>(
                fixture.Controller, "pauseCoordinator").HasPendingRestore, Is.False);
        }

        [UnityTest]
        public IEnumerator Controller_ExitReleaseCannotSelectWorld()
        {
            using var fixture = CreateControllerFixture();
            fixture.Registry.Rebuild(fixture.Layout.FurnitureInstances);
            Assert.That(fixture.Registry.TryGet(
                "00000000000000000000000000000001", out var formal), Is.True);
            var selectable = formal.GetComponentInChildren<Task4Selectable>();
            var screen = fixture.Camera.WorldToScreenPoint(selectable.transform.position);
            fixture.Controller.EnterDecorationMode();

            fixture.Controller.ExitDecorationMode();
            fixture.LegacyInput.NextFrame = new AnimalCafe.Input.CameraInputFrame(
                Vector2.zero,
                0f,
                true,
                screen,
                -1,
                pointerReleased: true);
            yield return null;

            Assert.That(fixture.SceneInteraction.CurrentSelection, Is.Null);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Controller_ExitRestoresPriorCameraEnabledState(bool initiallyEnabled)
        {
            using var fixture = CreateControllerFixture();
            fixture.CameraController.enabled = initiallyEnabled;

            fixture.Controller.EnterDecorationMode();
            Assert.That(fixture.CameraController.enabled, Is.False);
            fixture.Controller.ExitDecorationMode();

            Assert.That(fixture.CameraController.enabled, Is.EqualTo(initiallyEnabled));
        }

        [Test]
        public void Controller_HudToggleEntersAndExitsWithProvisionalLabels()
        {
            using var fixture = CreateControllerFixture();

            Assert.That(fixture.HudLabel.text, Is.EqualTo("Decoration"));

            fixture.HudButton.onClick.Invoke();

            Assert.That(fixture.Controller.IsOpen, Is.True);
            Assert.That(fixture.HudLabel.text, Is.EqualTo("Done"));

            fixture.HudButton.onClick.Invoke();

            Assert.That(fixture.Controller.IsOpen, Is.False);
            Assert.That(fixture.HudLabel.text, Is.EqualTo("Decoration"));
        }

        [Test]
        public void Controller_HudToggleFailedEnterRestoresClosedLabel()
        {
            using var fixture = CreateControllerFixture();
            fixture.BreakCatalogueReferences();

            Assert.Throws<InvalidOperationException>(() => fixture.HudButton.onClick.Invoke());

            fixture.AssertClosedAndClean();
            Assert.That(fixture.HudLabel.text, Is.EqualTo("Decoration"));
        }

        [Test]
        public void Controller_HudToggleReenableDoesNotDuplicateListener()
        {
            using var fixture = CreateControllerFixture();
            fixture.Controller.enabled = false;
            fixture.Controller.enabled = true;
            var requestsBeforeClick = fixture.Time.SetRequests;

            fixture.HudButton.onClick.Invoke();

            Assert.That(fixture.Controller.IsOpen, Is.True);
            Assert.That(fixture.HudLabel.text, Is.EqualTo("Done"));
            Assert.That(fixture.Time.SetRequests, Is.EqualTo(requestsBeforeClick + 1));
        }

        [Test]
        public void RouterConstruction_UsesCameraSettingsThresholdAndOneSanitizedOffset()
        {
            using var fixture = CreateControllerFixture();
            fixture.CameraSettings.DragThresholdPixels = 37f;
            fixture.SetFurnitureOffset(24f);

            fixture.Controller.EnterDecorationMode();

            var router = GetField<DecorationTouchRouter>(fixture.Controller, "touchRouter");
            Assert.That(GetField<float>(router, "dragThresholdPixels"), Is.EqualTo(37f));
            Assert.That(GetField<float>(router, "furnitureDragOffsetPixels"), Is.EqualTo(24f));
            Assert.That(GetField<float>(fixture.Controller, "sanitizedFurnitureDragOffsetPixels"),
                Is.EqualTo(24f));
        }

        [TestCase(-10f, 0f)]
        [TestCase(float.NaN, 0f)]
        [TestCase(float.PositiveInfinity, 0f)]
        [TestCase(18f, 18f)]
        public void Controller_OffsetSanitizesNegativeAndNonfiniteOnce(
            float configured,
            float expected)
        {
            using var fixture = CreateControllerFixture();
            fixture.SetFurnitureOffset(configured);

            fixture.Controller.EnterDecorationMode();

            Assert.That(GetField<float>(fixture.Controller, "sanitizedFurnitureDragOffsetPixels"),
                Is.EqualTo(expected));
            var router = GetField<DecorationTouchRouter>(fixture.Controller, "touchRouter");
            Assert.That(GetField<float>(router, "furnitureDragOffsetPixels"), Is.EqualTo(expected));
        }

        [TestCase(0.85f, 0.85f)]
        [TestCase(0f, 0f)]
        [TestCase(-0.5f, 0f)]
        [TestCase(float.NaN, 0f)]
        [TestCase(float.PositiveInfinity, 0f)]
        [TestCase(float.NegativeInfinity, 0f)]
        public void Controller_HoverHeightAffectsPresentationOnly(
            float configured,
            float expected)
        {
            using var fixture = CreateControllerFixture();
            fixture.SetHoverHeight(configured);
            fixture.Controller.EnterDecorationMode();
            fixture.SelectExisting("00000000000000000000000000000001");
            var preview = fixture.PreviewRoot.GetChild(0);
            var formalBefore = fixture.Layout.FurnitureInstances[0];

            Assert.That(GetField<float>(fixture.Controller, "sanitizedFurnitureHoverHeight"),
                Is.EqualTo(expected));
            Assert.That(preview.localPosition.y, Is.EqualTo(expected).Within(Epsilon));
            Assert.That(preview.localPosition.x,
                Is.EqualTo(fixture.GridSpace.GetCellCenterLocal(new GridPosition(2, 3)).x)
                    .Within(Epsilon));
            Assert.That(preview.localPosition.z,
                Is.EqualTo(fixture.GridSpace.GetCellCenterLocal(new GridPosition(2, 3)).z)
                    .Within(Epsilon));
            Assert.That(fixture.Session.ActivePreview.ProposedPosition,
                Is.EqualTo(new GridPosition(2, 3)));
            Assert.That(fixture.Layout.FurnitureInstances[0], Is.SameAs(formalBefore));
        }

        [Test]
        public void BlankSceneDrag_PansCameraWithoutFurnitureMutation()
        {
            using var fixture = CreateControllerFixture();
            fixture.Controller.EnterDecorationMode();
            var beforeCamera = fixture.Camera.transform.position;
            var before = fixture.Layout.FurnitureInstances[0];
            var blank = fixture.ScreenForCell(new GridPosition(7, 7));

            fixture.SendTouch(31, blank, Vector2.zero, UnityEngine.InputSystem.TouchPhase.Began);
            fixture.SendTouch(31, blank + new Vector2(80f, 30f), new Vector2(80f, 30f),
                UnityEngine.InputSystem.TouchPhase.Moved);

            Assert.That(fixture.Camera.transform.position, Is.Not.EqualTo(beforeCamera));
            Assert.That(fixture.Session.ActivePreview, Is.Null);
            Assert.That(fixture.Layout.FurnitureInstances[0], Is.SameAs(before));
            Assert.That(fixture.Layout.OccupiedCellCount, Is.EqualTo(1));
        }

        [Test]
        public void FurnitureDrag_OffsetAffectsGridProjectionButNotEdgeZone()
        {
            using var fixture = CreateControllerFixture();
            fixture.SetFurnitureOffset(120f);
            fixture.Controller.EnterDecorationMode();
            fixture.SelectCatalogue(0);
            var raw = fixture.ScreenForCell(new GridPosition(4, 3));

            fixture.SendTouch(32, raw, Vector2.zero, UnityEngine.InputSystem.TouchPhase.Began);
            fixture.SendTouch(32, raw + Vector2.right * 40f, Vector2.right * 40f,
                UnityEngine.InputSystem.TouchPhase.Moved);

            var offsetProjected = fixture.ProjectScreen(raw + Vector2.right * 40f + Vector2.up * 120f);
            Assert.That(fixture.Session.ActivePreview.ProposedPosition, Is.EqualTo(offsetProjected));
            Assert.That(fixture.CameraDriver.IsEdgeAutoPanning, Is.False,
                "The raw finger remains away from the edge even if the visual offset is nearer it.");
        }

        [Test]
        public void FurnitureDrag_SameSnappedCellDoesNotRepublishPreview()
        {
            using var fixture = CreateControllerFixture();
            fixture.Controller.EnterDecorationMode();
            fixture.SelectCatalogue(0);
            var before = fixture.Session.ActivePreview;
            var center = fixture.ScreenForCell(before.ProposedPosition);

            fixture.SendTouch(38, center, Vector2.zero,
                UnityEngine.InputSystem.TouchPhase.Began);
            fixture.SendTouch(38, center + Vector2.right * 7f, Vector2.right * 7f,
                UnityEngine.InputSystem.TouchPhase.Moved);

            Assert.That(fixture.Session.ActivePreview, Is.SameAs(before),
                "A drag frame inside the same snapped cell must not republish domain/view state.");
        }

        [UnityTest]
        public IEnumerator FurnitureDrag_OffsetDoesNotMoveUiExclusionHitRegion()
        {
            using var fixture = CreateControllerFixture();
            fixture.SetFurnitureOffset(160f);
            fixture.Controller.EnterDecorationMode();
            fixture.SelectCatalogue(0);
            var raw = fixture.ScreenForCell(fixture.Session.ActivePreview.ProposedPosition);
            fixture.SendTouch(33, raw, Vector2.zero, UnityEngine.InputSystem.TouchPhase.Began);
            fixture.ShowUiOverlay(raw);
            yield return null;
            var before = fixture.Session.ActivePreview.ProposedPosition;

            fixture.SendTouch(33, raw + Vector2.right * 45f, Vector2.right * 45f,
                UnityEngine.InputSystem.TouchPhase.Moved);

            Assert.That(fixture.Session.ActivePreview.ProposedPosition, Is.EqualTo(before));
            Assert.That(fixture.CameraDriver.IsEdgeAutoPanning, Is.False);
        }

        [UnityTest]
        public IEnumerator FurnitureDrag_ModalOrActionBarStopsEdgePanImmediately()
        {
            using var fixture = CreateControllerFixture();
            fixture.Controller.EnterDecorationMode();
            fixture.SelectExisting("00000000000000000000000000000001");
            var edge = new Vector2(fixture.Camera.pixelRect.xMax - 2f,
                fixture.Camera.pixelRect.center.y);
            fixture.SendTouch(34, fixture.ScreenForCell(new GridPosition(2, 3)), Vector2.zero,
                UnityEngine.InputSystem.TouchPhase.Began);
            fixture.SendTouch(34, edge, Vector2.right * 100f,
                UnityEngine.InputSystem.TouchPhase.Moved);
            Assert.That(fixture.CameraDriver.IsEdgeAutoPanning, Is.True);

            fixture.Action.ShowRaycastRegion(edge);
            yield return null;
            fixture.SendTouch(34, edge, Vector2.zero,
                UnityEngine.InputSystem.TouchPhase.Stationary);
            Assert.That(fixture.CameraDriver.IsEdgeAutoPanning, Is.False,
                "An active action-bar GraphicRaycaster hit must stop edge auto-pan.");

            fixture.Action.HideRaycastRegion();
            fixture.SendTouch(34, edge, Vector2.right,
                UnityEngine.InputSystem.TouchPhase.Moved);
            Assert.That(fixture.CameraDriver.IsEdgeAutoPanning, Is.True);

            var preview = fixture.Session.ActivePreview;
            var state = fixture.Controller.State;
            var layout = fixture.Layout.FurnitureInstances.ToArray();
            var storeRequestCount = 0;
            fixture.Action.View.StoreRequested += () => storeRequestCount++;
            fixture.TapActionBar(fixture.Action.Store, 3401);
            Assert.That(storeRequestCount, Is.EqualTo(1));
            Assert.That(fixture.Modal.View.IsOpen, Is.False,
                "Store presentation during Furniture ownership must not open the modal.");
            Assert.That(fixture.Session.ActivePreview, Is.SameAs(preview));
            Assert.That(fixture.Controller.State, Is.EqualTo(state));
            Assert.That(fixture.Layout.FurnitureInstances, Is.EqualTo(layout).AsCollection);
            Assert.That(fixture.CameraDriver.IsEdgeAutoPanning, Is.True,
                "A rejected secondary Store request must not terminate the primary drag.");

            fixture.SendTouch(34, edge, Vector2.zero,
                UnityEngine.InputSystem.TouchPhase.Ended);
            Assert.That(GetField<DecorationTouchRouter>(fixture.Controller, "touchRouter").Owner,
                Is.EqualTo(DecorationGestureOwner.None));
            Assert.That(fixture.CameraDriver.IsEdgeAutoPanning, Is.False);

            fixture.TapActionBar(fixture.Action.Store, 3402);
            Assert.That(storeRequestCount, Is.EqualTo(2));
            Assert.That(fixture.Modal.View.IsOpen, Is.True,
                "A fresh Store tap after the gesture ends must remain usable.");
        }

        [TestCase("Release")]
        [TestCase("Confirm")]
        [TestCase("Cancel")]
        public void FurnitureEdgePan_ReleaseConfirmOrCancelStopsImmediately(string terminal)
        {
            using var fixture = CreateControllerFixture();
            fixture.Controller.EnterDecorationMode();
            fixture.SelectCatalogue(0);
            var dragPosition = BeginFurnitureEdgePan(fixture, 39);
            Assert.That(fixture.Session.ActivePreview.PlacementResult.Succeeded, Is.True,
                "The terminal-action fixture must keep Confirm eligible.");
            var preview = fixture.Session.ActivePreview;
            var state = fixture.Controller.State;
            var layout = fixture.Layout.FurnitureInstances.ToArray();
            var requestCount = 0;

            switch (terminal)
            {
                case "Release":
                    fixture.SendTouch(39, dragPosition, Vector2.zero,
                        UnityEngine.InputSystem.TouchPhase.Ended);
                    break;
                case "Confirm":
                    fixture.Action.View.ConfirmRequested += () => requestCount++;
                    fixture.TapActionBar(fixture.Action.Confirm, 3901);
                    break;
                case "Cancel":
                    fixture.Action.View.CancelRequested += () => requestCount++;
                    fixture.TapActionBar(fixture.Action.Cancel, 3902);
                    break;
                default:
                    Assert.Fail("Unknown terminal action: " + terminal);
                    break;
            }

            if (terminal != "Release")
            {
                Assert.That(requestCount, Is.EqualTo(1));
                Assert.That(fixture.Session.ActivePreview, Is.SameAs(preview));
                Assert.That(fixture.Controller.State, Is.EqualTo(state));
                Assert.That(fixture.Layout.FurnitureInstances, Is.EqualTo(layout).AsCollection);
                Assert.That(fixture.CameraDriver.IsEdgeAutoPanning, Is.True,
                    "A rejected secondary terminal request must leave the primary drag active.");

                fixture.SendTouch(39, dragPosition, Vector2.zero,
                    UnityEngine.InputSystem.TouchPhase.Ended);
                Assert.That(GetField<DecorationTouchRouter>(fixture.Controller, "touchRouter").Owner,
                    Is.EqualTo(DecorationGestureOwner.None));

                if (terminal == "Confirm")
                {
                    fixture.TapActionBar(fixture.Action.Confirm, 3903);
                }
                else
                {
                    fixture.TapActionBar(fixture.Action.Cancel, 3904);
                }

                Assert.That(requestCount, Is.EqualTo(2));
                Assert.That(fixture.Controller.State,
                    Is.EqualTo(DecorationSessionState.BrowsingCatalogue));
                Assert.That(fixture.Session.ActivePreview, Is.Null,
                    "A fresh terminal tap after the gesture ends must execute exactly once.");
            }

            Assert.That(fixture.CameraDriver.IsEdgeAutoPanning, Is.False);
        }

        [UnityTest]
        public IEnumerator FurnitureEdgePan_ReprojectsOffsetPointAfterCameraMove()
        {
            using var fixture = CreateControllerFixture();
            fixture.CameraDriver.MaxEdgeSpeedPixelsPerSecond = 5000f;
            fixture.CameraDriver.EdgeZonePixels = 200f;
            fixture.Controller.EnterDecorationMode();
            fixture.SelectCatalogue(0);
            var start = fixture.ScreenForCell(fixture.Session.ActivePreview.ProposedPosition);
            var edge = new Vector2(fixture.Camera.pixelRect.xMax - 2f,
                fixture.Camera.pixelRect.center.y);
            fixture.SendTouch(35, start, Vector2.zero, UnityEngine.InputSystem.TouchPhase.Began);
            yield return null;
            fixture.SendTouch(35, edge, edge - start, UnityEngine.InputSystem.TouchPhase.Moved);
            var firstCell = fixture.Session.ActivePreview.ProposedPosition;
            var firstCamera = fixture.Camera.transform.position;
            yield return null;

            var projectedBeforeSecondPan = fixture.ProjectScreen(edge);

            var projectedAfterSecondPan = projectedBeforeSecondPan;
            var projectionDeadline = UnityEngine.Time.realtimeSinceStartup + 2f;
            while (projectedAfterSecondPan == projectedBeforeSecondPan
                && UnityEngine.Time.realtimeSinceStartup < projectionDeadline)
            {
                fixture.SendTouch(
                    35,
                    edge,
                    Vector2.zero,
                    UnityEngine.InputSystem.TouchPhase.Stationary);
                projectedAfterSecondPan = fixture.ProjectScreen(edge);
                yield return null;
            }

            Assert.That(fixture.Camera.transform.position, Is.Not.EqualTo(firstCamera));
            Assert.That(projectedAfterSecondPan, Is.Not.EqualTo(projectedBeforeSecondPan),
                "The test setup must cross a snapped-cell boundary during the second pan.");
            Assert.That(fixture.Session.ActivePreview.ProposedPosition, Is.EqualTo(firstCell),
                "A reprojected furniture preview must remain clamped at the floor edge.");
        }

        [TestCase("Pinch", "Rotate")]
        [TestCase("Pinch", "Cancel")]
        [TestCase("Pinch", "Confirm")]
        [TestCase("Pinch", "Store")]
        [TestCase("Camera", "Rotate")]
        [TestCase("Camera", "Cancel")]
        [TestCase("Camera", "Confirm")]
        [TestCase("Camera", "Store")]
        public void Controller_ActionBarRequestDuringNonUiOwnerIsPresentationOnlyAndFreshRequestStillWorks(
            string ownerName,
            string actionName)
        {
            using var fixture = CreateControllerFixture();
            fixture.Controller.EnterDecorationMode();
            fixture.SelectExisting("00000000000000000000000000000001");
            var preview = fixture.Session.ActivePreview;
            var previewCell = preview.ProposedPosition;
            var previewRotation = preview.ProposedRotation;
            var state = fixture.Controller.State;
            var layout = fixture.Layout.FurnitureInstances.ToArray();
            var selection = fixture.SceneInteraction.CurrentSelection;
            var cameraPosition = fixture.Camera.transform.position;
            var cameraSize = fixture.Camera.orthographicSize;
            var requestCount = 0;
            Action countRequest = () => requestCount++;
            Button actionButton;
            switch (actionName)
            {
                case "Rotate":
                    fixture.Action.View.RotateRequested += countRequest;
                    actionButton = fixture.Action.Rotate;
                    break;
                case "Cancel":
                    fixture.Action.View.CancelRequested += countRequest;
                    actionButton = fixture.Action.Cancel;
                    break;
                case "Confirm":
                    fixture.Action.View.ConfirmRequested += countRequest;
                    actionButton = fixture.Action.Confirm;
                    break;
                case "Store":
                    fixture.Action.View.StoreRequested += countRequest;
                    actionButton = fixture.Action.Store;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(actionName), actionName, null);
            }

            var primary = ownerName == "Camera"
                ? fixture.ScreenForCell(new GridPosition(7, 7))
                : fixture.ScreenForCell(previewCell);
            var secondary = primary + new Vector2(80f, 40f);
            fixture.SendTouch(91, primary, Vector2.zero,
                UnityEngine.InputSystem.TouchPhase.Began);
            if (ownerName == "Pinch")
            {
                fixture.SendTouches(
                    new DecorationTouchPoint(91, primary, Vector2.zero,
                        UnityEngine.InputSystem.TouchPhase.Stationary),
                    new DecorationTouchPoint(92, secondary, Vector2.zero,
                        UnityEngine.InputSystem.TouchPhase.Began));
            }
            Assert.That(GetField<DecorationTouchRouter>(fixture.Controller, "touchRouter").Owner,
                Is.EqualTo(ownerName == "Pinch"
                    ? DecorationGestureOwner.Pinch
                    : DecorationGestureOwner.Camera));

            actionButton.onClick.Invoke();

            Assert.That(requestCount, Is.EqualTo(1),
                "The ActionBar presentation request may still be emitted during " + ownerName + ".");
            Assert.That(fixture.Session.ActivePreview, Is.SameAs(preview));
            Assert.That(preview.ProposedPosition, Is.EqualTo(previewCell));
            Assert.That(preview.ProposedRotation, Is.EqualTo(previewRotation));
            Assert.That(fixture.Controller.State, Is.EqualTo(state));
            Assert.That(fixture.Layout.FurnitureInstances, Is.EqualTo(layout).AsCollection);
            Assert.That(fixture.SceneInteraction.CurrentSelection, Is.SameAs(selection));
            Assert.That(fixture.Camera.transform.position, Is.EqualTo(cameraPosition));
            Assert.That(fixture.Camera.orthographicSize, Is.EqualTo(cameraSize));
            Assert.That(fixture.Modal.View.IsOpen, Is.False);
            Assert.That(fixture.Action.View.IsVisible, Is.True,
                "A rejected terminal request must restore the same Action window and latch.");

            if (ownerName == "Pinch")
            {
                fixture.SendTouches(
                    new DecorationTouchPoint(91, primary, Vector2.zero,
                        UnityEngine.InputSystem.TouchPhase.Stationary),
                    new DecorationTouchPoint(92, secondary, Vector2.zero,
                        UnityEngine.InputSystem.TouchPhase.Ended));
            }
            fixture.SendTouch(91, primary, Vector2.zero,
                UnityEngine.InputSystem.TouchPhase.Ended);
            Assert.That(GetField<DecorationTouchRouter>(fixture.Controller, "touchRouter").Owner,
                Is.EqualTo(DecorationGestureOwner.None));

            actionButton.onClick.Invoke();
            Assert.That(requestCount, Is.EqualTo(2),
                "The restored Action window must accept a later fresh UI request exactly once.");
            switch (actionName)
            {
                case "Rotate":
                    Assert.That(fixture.Session.ActivePreview.ProposedRotation,
                        Is.Not.EqualTo(previewRotation));
                    break;
                case "Cancel":
                case "Confirm":
                    Assert.That(fixture.Controller.State,
                        Is.EqualTo(DecorationSessionState.BrowsingCatalogue));
                    Assert.That(fixture.Session.ActivePreview, Is.Null);
                    break;
                case "Store":
                    Assert.That(fixture.Controller.State,
                        Is.EqualTo(DecorationSessionState.ConfirmingStore));
                    Assert.That(fixture.Modal.View.IsOpen, Is.True);
                    break;
            }
        }

        [Test]
        public void Pinch_FreezesPreviewAndStopsEdgePanUntilResume()
        {
            using var fixture = CreateControllerFixture();
            fixture.CameraDriver.MaxEdgeSpeedPixelsPerSecond = 5000f;
            fixture.Controller.EnterDecorationMode();
            fixture.SelectCatalogue(0);
            var start = fixture.ScreenForCell(fixture.Session.ActivePreview.ProposedPosition);
            var edge = new Vector2(fixture.Camera.pixelRect.xMax - 2f,
                fixture.Camera.pixelRect.center.y);
            fixture.SendTouch(36, start, Vector2.zero, UnityEngine.InputSystem.TouchPhase.Began);
            fixture.SendTouch(36, edge, edge - start, UnityEngine.InputSystem.TouchPhase.Moved);
            var frozen = fixture.Session.ActivePreview.ProposedPosition;
            Assert.That(fixture.CameraDriver.IsEdgeAutoPanning, Is.True);

            fixture.SendTouches(
                new DecorationTouchPoint(36, edge, Vector2.zero,
                    UnityEngine.InputSystem.TouchPhase.Stationary),
                new DecorationTouchPoint(37, edge - Vector2.right * 100f, Vector2.zero,
                    UnityEngine.InputSystem.TouchPhase.Began));
            Assert.That(fixture.CameraDriver.IsEdgeAutoPanning, Is.False);
            var zoomBeforePinchMove = fixture.Camera.orthographicSize;
            fixture.SendTouches(
                new DecorationTouchPoint(36, edge, Vector2.zero,
                    UnityEngine.InputSystem.TouchPhase.Stationary),
                new DecorationTouchPoint(37, edge - Vector2.right * 150f, Vector2.left * 50f,
                    UnityEngine.InputSystem.TouchPhase.Moved));
            Assert.That(fixture.Session.ActivePreview.ProposedPosition, Is.EqualTo(frozen));
            Assert.That(fixture.Camera.orthographicSize,
                Is.EqualTo(zoomBeforePinchMove - fixture.CameraSettings.ZoomSpeed)
                    .Within(Epsilon),
                "The controller must route pinch distance through CameraDriver.ApplyPinchZoom.");

            fixture.SendTouches(
                new DecorationTouchPoint(36, edge, Vector2.zero,
                    UnityEngine.InputSystem.TouchPhase.Stationary),
                new DecorationTouchPoint(37, edge - Vector2.right * 150f, Vector2.zero,
                    UnityEngine.InputSystem.TouchPhase.Ended));
            fixture.SendTouch(36, edge - Vector2.left * 60f, Vector2.left * -60f,
                UnityEngine.InputSystem.TouchPhase.Moved);
            Assert.That(fixture.Session.ActivePreview.ProposedPosition, Is.EqualTo(frozen),
                "Resumed furniture input must keep an edge-clamped preview on the floor.");
        }

        [Test]
        public void Controller_MouseGestureKeepsDeviceFamilyUntilTerminalThenTouchCanAcquire()
        {
            using var fixture = CreateControllerFixture();
            fixture.Controller.EnterDecorationMode();
            var furnitureScreen = fixture.ScreenForCell(new GridPosition(2, 3));

            fixture.SendMouse(furnitureScreen, Vector2.zero,
                UnityEngine.InputSystem.TouchPhase.Began, true);
            Assert.That(GetField<DecorationTouchRouter>(fixture.Controller, "touchRouter").Owner,
                Is.EqualTo(DecorationGestureOwner.Furniture));
            Assert.That(GetField<DecorationTouchRouter>(fixture.Controller, "touchRouter")
                .PrimaryTouchId, Is.EqualTo(MouseDecorationInputSource.PointerId));

            fixture.Touch.Queue(9001, new[]
            {
                new DecorationTouchPoint(71, furnitureScreen, Vector2.zero,
                    UnityEngine.InputSystem.TouchPhase.Began)
            });
            fixture.SendMouse(furnitureScreen, Vector2.zero,
                UnityEngine.InputSystem.TouchPhase.Stationary, true);
            Assert.That(GetField<DecorationTouchRouter>(fixture.Controller, "touchRouter")
                .PrimaryTouchId, Is.EqualTo(MouseDecorationInputSource.PointerId),
                "Touch must not replace an active Mouse gesture.");

            fixture.SendMouse(furnitureScreen, Vector2.zero,
                UnityEngine.InputSystem.TouchPhase.Ended, false);
            Assert.That(GetField<DecorationTouchRouter>(fixture.Controller, "touchRouter").Owner,
                Is.EqualTo(DecorationGestureOwner.None));

            fixture.SendTouch(72, furnitureScreen, Vector2.zero,
                UnityEngine.InputSystem.TouchPhase.Began);
            Assert.That(GetField<DecorationTouchRouter>(fixture.Controller, "touchRouter")
                .PrimaryTouchId, Is.EqualTo(72));
        }

        [Test]
        public void Controller_MouseCanceledTerminalReleasesFamilyAndAllowsFreshMouseGesture()
        {
            using var fixture = CreateControllerFixture();
            fixture.Controller.EnterDecorationMode();
            var furnitureScreen = fixture.ScreenForCell(new GridPosition(2, 3));

            fixture.SendMouse(furnitureScreen, Vector2.zero,
                UnityEngine.InputSystem.TouchPhase.Began, true);
            Assert.That(GetField<DecorationTouchRouter>(fixture.Controller, "touchRouter").Owner,
                Is.EqualTo(DecorationGestureOwner.Furniture));

            fixture.SendMouse(furnitureScreen, Vector2.zero,
                UnityEngine.InputSystem.TouchPhase.Canceled, false);
            Assert.That(GetField<DecorationTouchRouter>(fixture.Controller, "touchRouter").Owner,
                Is.EqualTo(DecorationGestureOwner.None));
            Assert.That(GetField<object>(fixture.Controller, "activePointerDeviceFamily").ToString(),
                Is.EqualTo("None"));

            fixture.SendMouse(furnitureScreen, Vector2.zero,
                UnityEngine.InputSystem.TouchPhase.Began, true);
            Assert.That(GetField<DecorationTouchRouter>(fixture.Controller, "touchRouter")
                .PrimaryTouchId, Is.EqualTo(MouseDecorationInputSource.PointerId));
        }

        [Test]
        public void Controller_MouseWheelZoomsOnlyWhenNoPointerGestureIsActive()
        {
            using var fixture = CreateControllerFixture();
            fixture.Controller.EnterDecorationMode();
            var initialSize = fixture.Camera.orthographicSize;

            fixture.Mouse.QueueScroll(10f);
            fixture.InvokeControllerUpdate();
            Assert.That(fixture.Camera.orthographicSize, Is.Not.EqualTo(initialSize));
            var afterIdleWheel = fixture.Camera.orthographicSize;

            var furnitureScreen = fixture.ScreenForCell(new GridPosition(2, 3));
            fixture.SendMouse(furnitureScreen, Vector2.zero,
                UnityEngine.InputSystem.TouchPhase.Began, true);
            fixture.Mouse.QueueScroll(10f);
            fixture.InvokeControllerUpdate();
            Assert.That(fixture.Camera.orthographicSize, Is.EqualTo(afterIdleWheel),
                "Wheel zoom must not run during an active pointer gesture.");
        }

        [Test]
        public void Controller_ActivePreviewUsesNewAndExistingFloatingActionOrder()
        {
            using var fixture = CreateControllerFixture();
            fixture.Controller.EnterDecorationMode();

            fixture.SelectCatalogue(0);
            Assert.That(ActiveActionNames(fixture.Action.Root), Is.EqualTo(new[]
            {
                "CancelButton", "RotateButton", "ConfirmButton"
            }));

            fixture.Action.Cancel.onClick.Invoke();
            fixture.SelectExisting("00000000000000000000000000000001");
            Assert.That(ActiveActionNames(fixture.Action.Root), Is.EqualTo(new[]
            {
                "StoreButton", "CancelButton", "RotateButton", "ConfirmButton"
            }));
        }

        [Test]
        public void Controller_EnterAndCleanupResetMouseInputWithoutForcingSpeed()
        {
            using var fixture = CreateControllerFixture(initialSpeed: GameSpeed.Fast);
            fixture.Controller.EnterDecorationMode();
            Assert.That(fixture.Mouse.ResetRequests, Is.EqualTo(1));

            fixture.Controller.ExitDecorationMode();
            Assert.That(fixture.Mouse.ResetRequests, Is.EqualTo(2));
            Assert.That(fixture.Time.CurrentSpeed, Is.EqualTo(GameSpeed.Fast));

            fixture.Controller.ExitDecorationMode();
            Assert.That(fixture.Mouse.ResetRequests, Is.EqualTo(2),
                "Repeated cleanup remains a no-op.");
        }

        private static string[] ActiveActionNames(GameObject root)
        {
            return root.transform.Cast<Transform>()
                .Where(child => child.GetComponent<Button>() != null
                    && child.gameObject.activeSelf)
                .Select(child => child.name)
                .ToArray();
        }

        private static void AssertCatalogueCollapsedForPreview(Task7ControllerFixture fixture)
        {
            Assert.That(fixture.Catalogue.View.State,
                Is.EqualTo(DecorationCatalogueState.Collapsed));
            Assert.That(fixture.Catalogue.View.IsCatalogueVisible, Is.True);
            Assert.That(fixture.Catalogue.View.IsCollapsed, Is.True);
            Assert.That(fixture.Catalogue.Expanded.activeSelf, Is.False);
            Assert.That(fixture.Catalogue.Collapsed.activeSelf, Is.True,
                "An active preview keeps the catalogue handle available.");
        }

        private static Rect GetScreenRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(
                corners.Min(corner => corner.x),
                corners.Min(corner => corner.y),
                corners.Max(corner => corner.x),
                corners.Max(corner => corner.y));
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            for (var index = ownedRoots.Count - 1; index >= 0; index--)
            {
                if (ownedRoots[index] != null)
                {
                    UnityEngine.Object.Destroy(ownedRoots[index]);
                }
            }

            for (var index = ownedAssets.Count - 1; index >= 0; index--)
            {
                if (ownedAssets[index] != null)
                {
                    UnityEngine.Object.Destroy(ownedAssets[index]);
                }
            }

            ownedRoots.Clear();
            ownedAssets.Clear();
            yield return null;
        }

        [Test]
        public void GridSpace_NonZeroOriginAndNonUnitCellSizeMapSouthwestLocalCoordinates()
        {
            var space = CreateGridSpace(
                new GridPosition(10, -3),
                new GridSize(8, 8),
                1.5f);

            AssertVector(
                space.GetCellCenterLocal(new GridPosition(10, -3), 0.25f),
                new Vector3(0.75f, 0.25f, 0.75f));
            AssertVector(
                space.GetCellCenterLocal(new GridPosition(9, -4)),
                new Vector3(-0.75f, 0f, -0.75f));
            AssertVector(
                space.GetCellCenterLocal(new GridPosition(18, 5)),
                new Vector3(12.75f, 0f, 12.75f));
        }

        [Test]
        public void GridSpace_CurrentCellBoundsCenterSupportsInsideAndOutsideMultiCellFootprints()
        {
            var space = CreateGridSpace(
                new GridPosition(10, -3),
                new GridSize(8, 8),
                2f);

            AssertVector(
                space.GetFootprintCenterLocal(
                    Cells((9, -4), (10, -4), (11, -4)),
                    0.4f),
                new Vector3(1f, 0.4f, -1f));
            AssertVector(
                space.GetFootprintCenterLocal(
                    Cells((11, -2), (11, -1), (12, -2), (12, -1))),
                new Vector3(4f, 0f, 4f));
        }

        [Test]
        public void GridSpace_RejectsNullOrEmptyFootprintsAndMapsAllQuarterTurnsExactly()
        {
            var space = CreateGridSpace();

            Assert.Throws<ArgumentNullException>(
                () => space.GetFootprintCenterLocal(null));
            Assert.Throws<ArgumentException>(
                () => space.GetFootprintCenterLocal(Array.Empty<GridPosition>()));

            Assert.That(
                Quaternion.Angle(
                    space.GetLocalRotation(FurnitureRotation.Degrees0),
                    Quaternion.Euler(0f, 0f, 0f)),
                Is.LessThan(Epsilon));
            Assert.That(
                Quaternion.Angle(
                    space.GetLocalRotation(FurnitureRotation.Degrees90),
                    Quaternion.Euler(0f, 90f, 0f)),
                Is.LessThan(Epsilon));
            Assert.That(
                Quaternion.Angle(
                    space.GetLocalRotation(FurnitureRotation.Degrees180),
                    Quaternion.Euler(0f, 180f, 0f)),
                Is.LessThan(Epsilon));
            Assert.That(
                Quaternion.Angle(
                    space.GetLocalRotation(FurnitureRotation.Degrees270),
                    Quaternion.Euler(0f, 270f, 0f)),
                Is.LessThan(Epsilon));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => space.GetLocalRotation((FurnitureRotation)45));

            var original = space.GetLocalRotation(FurnitureRotation.Degrees0);
            for (var index = 0; index < 20; index++)
            {
                Assert.That(
                    Quaternion.Angle(
                        space.GetLocalRotation(FurnitureRotation.Degrees0),
                        original),
                    Is.LessThan(Epsilon),
                "Rotation mapping must be absolute and cannot accumulate drift.");
            }
        }

        [Test]
        public void GridSpace_RejectsDefaultZeroSizedBounds()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new DecorationGridSpace(new GridSettings(1f), default));
        }

        [Test]
        public void GridSpace_RejectsCoordinatesOutsideFiniteVectorRange()
        {
            var space = new DecorationGridSpace(
                new GridSettings(float.MaxValue),
                new LayoutBounds(
                    new GridPosition(int.MinValue, int.MinValue),
                    new GridSize(1, 1)));
            var extremeCell = new GridPosition(int.MaxValue, int.MaxValue);

            Assert.Throws<OverflowException>(
                () => space.GetCellCenterLocal(extremeCell));
            Assert.Throws<OverflowException>(
                () => space.GetFootprintCenterLocal(new[] { extremeCell }));
        }

        [Test]
        public void GridSpace_RejectsNonFiniteHeightForCellAndFootprintMappings()
        {
            var space = CreateGridSpace();
            var cell = new GridPosition(1, 1);
            var nonFiniteHeights = new[]
            {
                float.NaN,
                float.PositiveInfinity,
                float.NegativeInfinity
            };

            foreach (var height in nonFiniteHeights)
            {
                Assert.Throws<ArgumentOutOfRangeException>(
                    () => space.GetCellCenterLocal(cell, height));
                Assert.Throws<ArgumentOutOfRangeException>(
                    () => space.GetFootprintCenterLocal(new[] { cell }, height));
            }
        }

        [UnityTest]
        public IEnumerator Registry_CreateUpdateRemoveAndRebuildTwiceRemainIdempotent()
        {
            var prefab = CreateFurniturePrefab("Counter1x3", 1, 3);
            var definition = CreateDefinition(
                "counter.scene.1x3", 1, 3, prefab);
            var catalog = CreateContentCatalog(definition);
            var registryRoot = CreateRoot("FormalFurnitureRoot");
            var sentinel = new GameObject("InjectedSentinel");
            sentinel.transform.SetParent(registryRoot.transform, false);
            var owner = CreateRoot("RegistryOwner");
            var registry = owner.AddComponent<FurnitureSceneRegistry>();
            var space = CreateGridSpace();
            registry.Configure(catalog, registryRoot.transform, space);
            var instanceId = "00000000000000000000000000000001";
            var initial = FurnitureInstance.Restore(
                instanceId,
                definition.DefinitionId,
                new GridPosition(1, 2),
                FurnitureRotation.Degrees0);

            registry.Rebuild(new[] { initial });
            registry.Rebuild(new[] { initial });
            yield return null;

            Assert.That(DirectChildrenNamed(registryRoot.transform, "Furniture_").Count, Is.EqualTo(1));
            Assert.That(registry.TryGet(instanceId, out var representation), Is.True);
            AssertVector(
                representation.transform.localPosition,
                space.GetFootprintCenterLocal(Cells((1, 2), (1, 3), (1, 4))));
            Assert.That(
                Quaternion.Angle(
                    representation.transform.localRotation,
                    space.GetLocalRotation(FurnitureRotation.Degrees0)),
                Is.LessThan(Epsilon));

            representation.transform.localPosition = new Vector3(99f, 99f, 99f);
            representation.transform.localRotation = Quaternion.Euler(13f, 17f, 19f);
            Assert.That(initial.Position, Is.EqualTo(new GridPosition(1, 2)));
            Assert.That(initial.Rotation, Is.EqualTo(FurnitureRotation.Degrees0));
            var moved = FurnitureInstance.Restore(
                instanceId,
                definition.DefinitionId,
                new GridPosition(3, 4),
                FurnitureRotation.Degrees90);
            registry.Rebuild(new[] { moved });

            Assert.That(registry.TryGet(instanceId, out var updated), Is.True);
            Assert.That(updated, Is.SameAs(representation));
            AssertVector(
                updated.transform.localPosition,
                space.GetFootprintCenterLocal(Cells((3, 4), (4, 4), (5, 4))));
            Assert.That(
                Quaternion.Angle(
                    updated.transform.localRotation,
                    space.GetLocalRotation(FurnitureRotation.Degrees90)),
                Is.LessThan(Epsilon));
            Assert.That(initial.Position, Is.EqualTo(new GridPosition(1, 2)));
            Assert.That(initial.Rotation, Is.EqualTo(FurnitureRotation.Degrees0));

            registry.Rebuild(Array.Empty<FurnitureInstance>());
            Assert.That(representation.activeSelf, Is.False,
                "Removed owned representations must stop being visible/hittable in the same frame.");
            yield return null;
            Assert.That(registry.TryGet(instanceId, out _), Is.False);
            Assert.That(DirectChildrenNamed(registryRoot.transform, "Furniture_").Count, Is.Zero);
            Assert.That(sentinel, Is.Not.Null);
            Assert.That(sentinel.transform.parent, Is.SameAs(registryRoot.transform));

            registry.Rebuild(new[] { moved });
            yield return null;
            Assert.That(registry.TryGet(instanceId, out _), Is.True);
            registry.Remove(instanceId);
            Assert.That(ActiveNamedChildren(registryRoot.transform, "Furniture_"), Is.Zero);
            yield return null;
            Assert.That(registry.TryGet(instanceId, out _), Is.False);
            Assert.That(DirectChildrenNamed(registryRoot.transform, "Furniture_").Count, Is.Zero);
            Assert.That(sentinel, Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator Registry_RebuildRestoresVisibilityWithoutPersistentHiddenState()
        {
            var prefab = CreateFurniturePrefab("VisibilityCounter", 1, 1);
            var sourceScale = new Vector3(1.2f, 0.8f, 1.1f);
            prefab.transform.localScale = sourceScale;
            var definition = CreateDefinition(
                "counter.scene.visibility", 1, 1, prefab);
            var registryRoot = CreateRoot("VisibilityFormalRoot");
            var sentinel = new GameObject("InjectedSentinel");
            sentinel.transform.SetParent(registryRoot.transform, false);
            var registry = CreateRoot("VisibilityRegistryOwner")
                .AddComponent<FurnitureSceneRegistry>();
            registry.Configure(
                CreateContentCatalog(definition),
                registryRoot.transform,
                CreateGridSpace());
            var instance = FurnitureInstance.Restore(
                "00000000000000000000000000000031",
                definition.DefinitionId,
                new GridPosition(2, 3),
                FurnitureRotation.Degrees270);

            registry.Rebuild(new[] { instance });
            yield return null;
            Assert.That(registry.TryGet(instance.InstanceId, out var representation), Is.True);

            Assert.That(registry.SetRepresentationVisible(instance.InstanceId, false), Is.True);
            Assert.That(representation.activeSelf, Is.False,
                "The registry visibility API must hide its owned clone in the same frame.");
            Assert.That(
                registry.SetRepresentationVisible(
                    "ffffffffffffffffffffffffffffffff",
                    false),
                Is.False);
            Assert.Throws<ArgumentNullException>(
                () => registry.SetRepresentationVisible(null, false));

            registry.Rebuild(new[] { instance });
            Assert.That(representation.activeSelf, Is.True,
                "Rebuild is authoritative and must not persist Task 7's temporary hide.");
            AssertVector(representation.transform.localScale, sourceScale);

            representation.SetActive(false);
            registry.Rebuild(new[] { instance });
            Assert.That(representation.activeSelf, Is.True,
                "Rebuild must also repair an externally deactivated representation.");
            Assert.That(registryRoot, Is.Not.Null);
            Assert.That(sentinel, Is.Not.Null);
            Assert.That(sentinel.transform.parent, Is.SameAs(registryRoot.transform));
            Assert.That(prefab != null, Is.True);
            Assert.That(prefab.activeSelf, Is.False);
            AssertVector(prefab.transform.localScale, sourceScale);
        }

        [UnityTest]
        public IEnumerator Registry_RebuildRecreatesExternallyDestroyedRepresentation()
        {
            var prefab = CreateFurniturePrefab("DestroyedCounter", 1, 1);
            var definition = CreateDefinition(
                "counter.scene.destroyed", 1, 1, prefab);
            var registryRoot = CreateRoot("DestroyedFormalRoot");
            var sentinel = new GameObject("InjectedSentinel");
            sentinel.transform.SetParent(registryRoot.transform, false);
            var registry = CreateRoot("DestroyedRegistryOwner")
                .AddComponent<FurnitureSceneRegistry>();
            registry.Configure(
                CreateContentCatalog(definition),
                registryRoot.transform,
                CreateGridSpace());
            var instance = FurnitureInstance.Restore(
                "00000000000000000000000000000032",
                definition.DefinitionId,
                new GridPosition(4, 2),
                FurnitureRotation.Degrees90);

            registry.Rebuild(new[] { instance });
            Assert.That(registry.TryGet(instance.InstanceId, out var destroyed), Is.True);
            UnityEngine.Object.Destroy(destroyed);
            yield return null;
            Assert.That(destroyed == null, Is.True);

            registry.Rebuild(new[] { instance });
            Assert.That(registry.TryGet(instance.InstanceId, out var recreated), Is.True);
            Assert.That(recreated == null, Is.False);
            Assert.That(recreated.activeSelf, Is.True);
            Assert.That(ActiveNamedChildren(registryRoot.transform, "Furniture_"), Is.EqualTo(1));
            yield return null;
            Assert.That(DirectChildrenNamed(registryRoot.transform, "Furniture_"), Has.Count.EqualTo(1));
            Assert.That(registryRoot, Is.Not.Null);
            Assert.That(sentinel, Is.Not.Null);
            Assert.That(sentinel.transform.parent, Is.SameAs(registryRoot.transform));
            Assert.That(prefab != null, Is.True);
            Assert.That(prefab.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator Registry_EmptyRebuildAfterExternalDestructionRemovesRecordWithoutThrowing()
        {
            var prefab = CreateFurniturePrefab("DestroyedStoreCounter", 1, 1);
            var definition = CreateDefinition(
                "counter.scene.destroyed-store", 1, 1, prefab);
            var registryRoot = CreateRoot("DestroyedStoreFormalRoot");
            var sentinel = new GameObject("InjectedSentinel");
            sentinel.transform.SetParent(registryRoot.transform, false);
            var registry = CreateRoot("DestroyedStoreRegistryOwner")
                .AddComponent<FurnitureSceneRegistry>();
            registry.Configure(
                CreateContentCatalog(definition),
                registryRoot.transform,
                CreateGridSpace());
            var instance = FurnitureInstance.Restore(
                "00000000000000000000000000000036",
                definition.DefinitionId,
                new GridPosition(2, 2),
                FurnitureRotation.Degrees0);

            registry.Rebuild(new[] { instance });
            Assert.That(registry.TryGet(instance.InstanceId, out var destroyed), Is.True);
            UnityEngine.Object.Destroy(destroyed);
            yield return null;
            Assert.That(destroyed == null, Is.True);

            Assert.DoesNotThrow(
                () => registry.Rebuild(Array.Empty<FurnitureInstance>()));
            Assert.That(registry.TryGet(instance.InstanceId, out _), Is.False);
            Assert.That(DirectChildrenNamed(registryRoot.transform, "Furniture_"), Is.Empty);
            Assert.That(registryRoot, Is.Not.Null);
            Assert.That(sentinel, Is.Not.Null);
            Assert.That(sentinel.transform.parent, Is.SameAs(registryRoot.transform));
            Assert.That(prefab != null, Is.True);
            Assert.That(prefab.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator Registry_RemoveAfterExternalDestructionIsIdempotentAndDoesNotThrow()
        {
            var prefab = CreateFurniturePrefab("DestroyedRemoveCounter", 1, 1);
            var definition = CreateDefinition(
                "counter.scene.destroyed-remove", 1, 1, prefab);
            var registryRoot = CreateRoot("DestroyedRemoveFormalRoot");
            var sentinel = new GameObject("InjectedSentinel");
            sentinel.transform.SetParent(registryRoot.transform, false);
            var registry = CreateRoot("DestroyedRemoveRegistryOwner")
                .AddComponent<FurnitureSceneRegistry>();
            registry.Configure(
                CreateContentCatalog(definition),
                registryRoot.transform,
                CreateGridSpace());
            var instance = FurnitureInstance.Restore(
                "00000000000000000000000000000037",
                definition.DefinitionId,
                new GridPosition(3, 3),
                FurnitureRotation.Degrees0);

            registry.Rebuild(new[] { instance });
            Assert.That(registry.TryGet(instance.InstanceId, out var destroyed), Is.True);
            UnityEngine.Object.Destroy(destroyed);
            yield return null;
            Assert.That(destroyed == null, Is.True);

            Assert.DoesNotThrow(() => registry.Remove(instance.InstanceId));
            Assert.DoesNotThrow(() => registry.Remove(instance.InstanceId));
            Assert.That(registry.TryGet(instance.InstanceId, out _), Is.False);
            Assert.That(DirectChildrenNamed(registryRoot.transform, "Furniture_"), Is.Empty);
            Assert.That(registryRoot, Is.Not.Null);
            Assert.That(sentinel, Is.Not.Null);
            Assert.That(sentinel.transform.parent, Is.SameAs(registryRoot.transform));
            Assert.That(prefab != null, Is.True);
            Assert.That(prefab.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator Registry_RebuildRestoresPrefabAuthoredRootScaleAndAbsoluteTransform()
        {
            var prefab = CreateFurniturePrefab("ScaledCounter", 1, 3);
            var sourceScale = new Vector3(1.4f, 0.75f, 0.9f);
            prefab.transform.localScale = sourceScale;
            var definition = CreateDefinition(
                "counter.scene.scaled", 1, 3, prefab);
            var registryRoot = CreateRoot("ScaledFormalRoot");
            var sentinel = new GameObject("InjectedSentinel");
            sentinel.transform.SetParent(registryRoot.transform, false);
            var registry = CreateRoot("ScaledRegistryOwner")
                .AddComponent<FurnitureSceneRegistry>();
            var space = CreateGridSpace();
            registry.Configure(
                CreateContentCatalog(definition),
                registryRoot.transform,
                space);
            var instance = FurnitureInstance.Restore(
                "00000000000000000000000000000033",
                definition.DefinitionId,
                new GridPosition(2, 4),
                FurnitureRotation.Degrees270);

            registry.Rebuild(new[] { instance });
            yield return null;
            Assert.That(registry.TryGet(instance.InstanceId, out var representation), Is.True);
            representation.transform.localScale = new Vector3(9f, 8f, 7f);
            representation.transform.localPosition = new Vector3(99f, 98f, 97f);
            representation.transform.localRotation = Quaternion.Euler(23f, 47f, 61f);

            registry.Rebuild(new[] { instance });

            AssertVector(representation.transform.localScale, sourceScale);
            AssertVector(
                representation.transform.localPosition,
                space.GetFootprintCenterLocal(Cells((2, 4), (3, 4), (4, 4))));
            Assert.That(
                Quaternion.Angle(
                    representation.transform.localRotation,
                    space.GetLocalRotation(FurnitureRotation.Degrees270)),
                Is.LessThan(Epsilon));
            Assert.That(instance.Position, Is.EqualTo(new GridPosition(2, 4)));
            Assert.That(instance.Rotation, Is.EqualTo(FurnitureRotation.Degrees270));
            Assert.That(registryRoot, Is.Not.Null);
            Assert.That(sentinel, Is.Not.Null);
            Assert.That(prefab != null, Is.True);
            AssertVector(prefab.transform.localScale, sourceScale);
        }

        [UnityTest]
        public IEnumerator Registry_DefinitionChangeImmediatelyReplacesCloneAndPreservesSources()
        {
            var prefabA = CreateFurniturePrefab("DefinitionSourceA", 1, 1);
            var prefabB = CreateFurniturePrefab("DefinitionSourceB", 2, 1);
            var definitionA = CreateDefinition(
                "counter.scene.definition-a", 1, 1, prefabA);
            var definitionB = CreateDefinition(
                "counter.scene.definition-b", 2, 1, prefabB);
            var registryRoot = CreateRoot("DefinitionChangeFormalRoot");
            var registry = CreateRoot("DefinitionChangeRegistryOwner")
                .AddComponent<FurnitureSceneRegistry>();
            var space = CreateGridSpace();
            registry.Configure(
                CreateContentCatalog(definitionA, definitionB),
                registryRoot.transform,
                space);
            const string instanceId = "00000000000000000000000000000034";
            var initial = FurnitureInstance.Restore(
                instanceId,
                definitionA.DefinitionId,
                new GridPosition(1, 1),
                FurnitureRotation.Degrees0);
            var changed = FurnitureInstance.Restore(
                instanceId,
                definitionB.DefinitionId,
                new GridPosition(3, 2),
                FurnitureRotation.Degrees180);

            registry.Rebuild(new[] { initial });
            Assert.That(registry.TryGet(instanceId, out var oldRepresentation), Is.True);
            registry.Rebuild(new[] { changed });

            Assert.That(oldRepresentation.activeSelf, Is.False);
            Assert.That(registry.TryGet(instanceId, out var newRepresentation), Is.True);
            Assert.That(newRepresentation, Is.Not.SameAs(oldRepresentation));
            Assert.That(newRepresentation.activeSelf, Is.True);
            Assert.That(ActiveNamedChildren(registryRoot.transform, "Furniture_"), Is.EqualTo(1));
            Assert.That(
                newRepresentation.GetComponentsInChildren<Renderer>(true),
                Has.Length.EqualTo(2));
            AssertVector(
                newRepresentation.transform.localPosition,
                space.GetFootprintCenterLocal(Cells((3, 2), (4, 2))));
            Assert.That(
                Quaternion.Angle(
                    newRepresentation.transform.localRotation,
                    space.GetLocalRotation(FurnitureRotation.Degrees180)),
                Is.LessThan(Epsilon));

            yield return null;
            Assert.That(oldRepresentation == null, Is.True);
            Assert.That(DirectChildrenNamed(registryRoot.transform, "Furniture_"), Has.Count.EqualTo(1));
            Assert.That(prefabA != null, Is.True);
            Assert.That(prefabB != null, Is.True);
            Assert.That(prefabA.activeSelf, Is.False);
            Assert.That(prefabB.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator Registry_PrefabChangeImmediatelyReplacesCloneAndPreservesSources()
        {
            var prefabA = CreateFurniturePrefab("PrefabSourceA", 1, 1);
            var prefabB = CreateFurniturePrefab("PrefabSourceB", 1, 2);
            var definition = CreateDefinition(
                "counter.scene.prefab-swap", 1, 1, prefabA);
            var registryRoot = CreateRoot("PrefabChangeFormalRoot");
            var registry = CreateRoot("PrefabChangeRegistryOwner")
                .AddComponent<FurnitureSceneRegistry>();
            var space = CreateGridSpace();
            registry.Configure(
                CreateContentCatalog(definition),
                registryRoot.transform,
                space);
            var instance = FurnitureInstance.Restore(
                "00000000000000000000000000000035",
                definition.DefinitionId,
                new GridPosition(5, 6),
                FurnitureRotation.Degrees90);

            registry.Rebuild(new[] { instance });
            Assert.That(registry.TryGet(instance.InstanceId, out var oldRepresentation), Is.True);
            SetField(definition, "prefab", prefabB);
            registry.Rebuild(new[] { instance });

            Assert.That(oldRepresentation.activeSelf, Is.False);
            Assert.That(registry.TryGet(instance.InstanceId, out var newRepresentation), Is.True);
            Assert.That(newRepresentation, Is.Not.SameAs(oldRepresentation));
            Assert.That(newRepresentation.activeSelf, Is.True);
            Assert.That(ActiveNamedChildren(registryRoot.transform, "Furniture_"), Is.EqualTo(1));
            Assert.That(
                newRepresentation.GetComponentsInChildren<Renderer>(true),
                Has.Length.EqualTo(2));
            AssertVector(
                newRepresentation.transform.localPosition,
                space.GetCellCenterLocal(new GridPosition(5, 6)));
            Assert.That(
                Quaternion.Angle(
                    newRepresentation.transform.localRotation,
                    space.GetLocalRotation(FurnitureRotation.Degrees90)),
                Is.LessThan(Epsilon));

            yield return null;
            Assert.That(oldRepresentation == null, Is.True);
            Assert.That(DirectChildrenNamed(registryRoot.transform, "Furniture_"), Has.Count.EqualTo(1));
            Assert.That(prefabA != null, Is.True);
            Assert.That(prefabB != null, Is.True);
            Assert.That(prefabA.activeSelf, Is.False);
            Assert.That(prefabB.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator Registry_StructuredIssuesAreSpecificAndValidSiblingsSurvive()
        {
            var validPrefab = CreateFurniturePrefab("ValidCounter", 1, 1);
            var validDefinition = CreateDefinition(
                "counter.scene.valid", 1, 1, validPrefab);
            var missingPrefabDefinition = CreateDefinition(
                "counter.scene.no-prefab", 1, 1, null);
            var catalog = CreateContentCatalog(validDefinition, missingPrefabDefinition);
            var registryRoot = CreateRoot("FormalFurnitureIssueRoot");
            var registry = CreateRoot("RegistryIssueOwner")
                .AddComponent<FurnitureSceneRegistry>();
            registry.Configure(catalog, registryRoot.transform, CreateGridSpace());

            var valid = FurnitureInstance.Restore(
                "00000000000000000000000000000011",
                validDefinition.DefinitionId,
                new GridPosition(1, 1),
                FurnitureRotation.Degrees0);
            var missingDefinition = FurnitureInstance.Restore(
                "00000000000000000000000000000012",
                "counter.scene.missing",
                new GridPosition(2, 1),
                FurnitureRotation.Degrees0);
            var missingPrefab = FurnitureInstance.Restore(
                "00000000000000000000000000000013",
                missingPrefabDefinition.DefinitionId,
                new GridPosition(3, 1),
                FurnitureRotation.Degrees0);
            var duplicateA = FurnitureInstance.Restore(
                "00000000000000000000000000000014",
                validDefinition.DefinitionId,
                new GridPosition(4, 1),
                FurnitureRotation.Degrees0);
            var duplicateB = FurnitureInstance.Restore(
                duplicateA.InstanceId,
                validDefinition.DefinitionId,
                new GridPosition(5, 1),
                FurnitureRotation.Degrees0);

            LogAssert.Expect(
                LogType.Error,
                new Regex("^" + Regex.Escape(
                    $"[{FurnitureSceneIssue.MissingDefinitionLogCode}] " +
                    $"Instance '{missingDefinition.InstanceId}' references missing Definition " +
                    $"'{missingDefinition.DefinitionId}'.") + "$"));
            LogAssert.Expect(
                LogType.Error,
                new Regex("^" + Regex.Escape(
                    $"[{FurnitureSceneIssue.MissingPrefabLogCode}] " +
                    $"Instance '{missingPrefab.InstanceId}' Definition " +
                    $"'{missingPrefab.DefinitionId}' has no Prefab.") + "$"));
            LogAssert.Expect(
                LogType.Error,
                new Regex("^" + Regex.Escape(
                    $"[{FurnitureSceneIssue.DuplicateInstanceIdLogCode}] " +
                    $"Instance ID '{duplicateA.InstanceId}' appears more than once.") + "$"));

            registry.Rebuild(new[]
            {
                valid,
                missingDefinition,
                missingPrefab,
                duplicateA,
                duplicateB
            });
            yield return null;

            Assert.That(registry.LastIssues, Has.Count.EqualTo(3));
            AssertIssue(
                registry.LastIssues,
                FurnitureSceneIssueCode.MissingDefinition,
                FurnitureSceneIssue.MissingDefinitionLogCode,
                missingDefinition.InstanceId,
                missingDefinition.DefinitionId);
            AssertIssue(
                registry.LastIssues,
                FurnitureSceneIssueCode.MissingPrefab,
                FurnitureSceneIssue.MissingPrefabLogCode,
                missingPrefab.InstanceId,
                missingPrefab.DefinitionId);
            AssertIssue(
                registry.LastIssues,
                FurnitureSceneIssueCode.DuplicateInstanceId,
                FurnitureSceneIssue.DuplicateInstanceIdLogCode,
                duplicateA.InstanceId,
                duplicateA.DefinitionId);

            Assert.That(registry.TryGet(valid.InstanceId, out var validObject), Is.True);
            Assert.That(validObject, Is.Not.Null);
            Assert.That(registry.TryGet(missingDefinition.InstanceId, out _), Is.False);
            Assert.That(registry.TryGet(missingPrefab.InstanceId, out _), Is.False);
            Assert.That(registry.TryGet(duplicateA.InstanceId, out _), Is.False);
            Assert.That(registryRoot.transform.childCount, Is.EqualTo(1));

            registry.Rebuild(new[] { valid });
            Assert.That(
                registry.LastIssues,
                Is.Empty,
                "LastIssues must describe only the latest rebuild.");
        }

        [UnityTest]
        public IEnumerator Registry_ChildHitReverseLookupAndDisableCleanupDoNotDestroyInjectedObjects()
        {
            var prefab = CreateFurniturePrefab("SelectableCounter", 1, 1);
            var definition = CreateDefinition(
                "counter.scene.selectable", 1, 1, prefab);
            var catalog = CreateContentCatalog(definition);
            var registryRoot = CreateRoot("InjectedFormalRoot");
            var sentinel = new GameObject("InjectedSentinel");
            sentinel.transform.SetParent(registryRoot.transform, false);
            var owner = CreateRoot("RegistryLifecycleOwner");
            var registry = owner.AddComponent<FurnitureSceneRegistry>();
            registry.Configure(catalog, registryRoot.transform, CreateGridSpace());
            var instance = FurnitureInstance.Restore(
                "00000000000000000000000000000021",
                definition.DefinitionId,
                new GridPosition(1, 1),
                FurnitureRotation.Degrees0);
            registry.Rebuild(new[] { instance });
            yield return null;

            Assert.That(registry.TryGet(instance.InstanceId, out var representation), Is.True);
            var childRenderer = representation.GetComponentInChildren<Renderer>();
            Assert.That(
                registry.TryGetInstanceId(childRenderer, out var hitInstanceId),
                Is.True);
            Assert.That(hitInstanceId, Is.EqualTo(instance.InstanceId));
            Assert.That(
                registry.TryGetInstanceId(registryRoot.transform, out _),
                Is.False);

            registry.enabled = false;
            yield return null;
            Assert.That(registryRoot, Is.Not.Null);
            Assert.That(prefab, Is.Not.Null);
            Assert.That(catalog, Is.Not.Null);
            Assert.That(DirectChildrenNamed(registryRoot.transform, "Furniture_").Count, Is.Zero);
            Assert.That(sentinel, Is.Not.Null);
            Assert.That(sentinel.transform.parent, Is.SameAs(registryRoot.transform));

            registry.enabled = true;
            registry.Rebuild(new[] { instance });
            yield return null;
            Assert.That(DirectChildrenNamed(registryRoot.transform, "Furniture_").Count, Is.EqualTo(1));

            UnityEngine.Object.Destroy(owner);
            yield return null;
            Assert.That(registryRoot, Is.Not.Null);
            Assert.That(prefab, Is.Not.Null);
            Assert.That(DirectChildrenNamed(registryRoot.transform, "Furniture_").Count, Is.Zero);
            Assert.That(sentinel, Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator Preview_OneCloneRecentersCurrentCellsWithoutRecreationAndHoverIsYOnly()
        {
            var prefab = CreateFurniturePrefab("PreviewCounter", 2, 3);
            var previewRoot = CreateRoot("PreviewRoot");
            var owner = CreateRoot("PreviewOwner");
            var preview = owner.AddComponent<FurniturePreviewView>();
            var space = CreateGridSpace();
            preview.Configure(previewRoot.transform, space, theme);

            preview.Show(prefab, Cells((1, 1)));
            yield return null;
            Assert.That(ActiveDirectChildren(previewRoot.transform), Is.EqualTo(1));
            var clone = ActiveChild(previewRoot.transform).gameObject;

            var centeredThreeCellFootprints = new[]
            {
                Cells((3, 3), (3, 4), (3, 5)),
                Cells((2, 4), (3, 4), (4, 4)),
                Cells((3, 3), (3, 4), (3, 5)),
                Cells((2, 4), (3, 4), (4, 4)),
                Cells((3, 3), (3, 4), (3, 5))
            };
            var rotations = new[]
            {
                FurnitureRotation.Degrees0,
                FurnitureRotation.Degrees90,
                FurnitureRotation.Degrees180,
                FurnitureRotation.Degrees270,
                FurnitureRotation.Degrees0
            };

            var originalCenter = space.GetFootprintCenterLocal(
                centeredThreeCellFootprints[0], 0.35f);
            for (var index = 0; index < centeredThreeCellFootprints.Length; index++)
            {
                preview.SetPlacement(
                    centeredThreeCellFootprints[index],
                    rotations[index],
                    0.35f);
                Assert.That(ActiveChild(previewRoot.transform).gameObject, Is.SameAs(clone));
                AssertVector(
                    clone.transform.localPosition,
                    originalCenter);
                Assert.That(
                    Quaternion.Angle(
                    clone.transform.localRotation,
                        space.GetLocalRotation(rotations[index])),
                    Is.LessThan(Epsilon));
            }

            var twoByThree = Cells(
                (1, 4), (1, 5), (1, 6),
                (2, 4), (2, 5), (2, 6));
            preview.SetPlacement(twoByThree, FurnitureRotation.Degrees0, 0.35f);
            var beforeHoverChange = clone.transform.localPosition;
            preview.SetPlacement(twoByThree, FurnitureRotation.Degrees0, 0.8f);
            Assert.That(clone.transform.localPosition.x, Is.EqualTo(beforeHoverChange.x).Within(Epsilon));
            Assert.That(clone.transform.localPosition.z, Is.EqualTo(beforeHoverChange.z).Within(Epsilon));
            Assert.That(clone.transform.localPosition.y, Is.EqualTo(0.8f).Within(Epsilon));

            preview.Show(prefab, Cells((6, 6)));
            yield return null;
            Assert.That(ActiveDirectChildren(previewRoot.transform), Is.EqualTo(1));
            Assert.That(ActiveChild(previewRoot.transform).gameObject, Is.Not.SameAs(clone));
        }

        [UnityTest]
        public IEnumerator Preview_DisablesCollidersAndSelectableBehavioursAndUsesPropertyBlocks()
        {
            var prefab = CreateFurniturePrefab("PreviewSafetyCounter", 1, 3);
            var previewRoot = CreateRoot("PreviewSafetyRoot");
            var preview = CreateRoot("PreviewSafetyOwner")
                .AddComponent<FurniturePreviewView>();
            preview.Configure(previewRoot.transform, CreateGridSpace(), theme);
            var sharedColorBefore = ReadMaterialColor(worldMaterial);
            var sourceColliders = prefab.GetComponentsInChildren<Collider>(true);
            var sourceSelectables = prefab.GetComponentsInChildren<MonoBehaviour>(true)
                .Where(behaviour => behaviour is ISelectable)
                .ToArray();
            Assert.That(sourceColliders, Is.Not.Empty);
            Assert.That(sourceColliders, Has.All.Matches<Collider>(collider => collider.enabled));
            Assert.That(sourceSelectables, Has.Length.EqualTo(2));
            Assert.That(sourceSelectables, Has.All.Matches<MonoBehaviour>(behaviour => behaviour.enabled));

            preview.Show(prefab, Cells((1, 1)));
            preview.SetValidity(true);
            yield return null;

            var clone = ActiveChild(previewRoot.transform).gameObject;
            Assert.That(
                clone.GetComponentsInChildren<Collider>(true),
                Has.All.Matches<Collider>(collider => !collider.enabled));
            Assert.That(
                clone.GetComponentsInChildren<MonoBehaviour>(true)
                    .Where(behaviour => behaviour is ISelectable),
                Has.All.Matches<MonoBehaviour>(behaviour => !behaviour.enabled));

            var renderers = clone.GetComponentsInChildren<Renderer>(true);
            Assert.That(renderers, Has.Length.EqualTo(3));
            foreach (var renderer in renderers)
            {
                AssertRendererColor(renderer, theme.Colors.Accent);
            }
            Assert.That(ReadMaterialColor(worldMaterial), Is.EqualTo(sharedColorBefore));

            preview.SetValidity(false);
            foreach (var renderer in renderers)
            {
                AssertRendererColor(renderer, theme.Colors.Destructive);
            }
            Assert.That(ReadMaterialColor(worldMaterial), Is.EqualTo(sharedColorBefore));
            Assert.That(sourceColliders, Has.All.Matches<Collider>(collider => collider.enabled));
            Assert.That(sourceSelectables, Has.All.Matches<MonoBehaviour>(behaviour => behaviour.enabled));
        }

        [UnityTest]
        public IEnumerator Preview_HideDisableAndDestroyAreIdempotentAndLeaveNoStaleClone()
        {
            var prefab = CreateFurniturePrefab("PreviewLifecycleCounter", 1, 1);
            var previewRoot = CreateRoot("PreviewLifecycleRoot");
            var owner = CreateRoot("PreviewLifecycleOwner");
            var preview = owner.AddComponent<FurniturePreviewView>();
            preview.Configure(previewRoot.transform, CreateGridSpace(), theme);

            preview.Show(prefab, Cells((1, 1)));
            preview.Hide();
            Assert.That(ActiveDirectChildren(previewRoot.transform), Is.Zero,
                "Hide must make the old clone non-visible in the same frame.");
            preview.Hide();
            yield return null;
            Assert.That(previewRoot.transform.childCount, Is.Zero);

            preview.Show(prefab, Cells((2, 2)));
            preview.enabled = false;
            yield return null;
            Assert.That(previewRoot.transform.childCount, Is.Zero);
            Assert.That(prefab, Is.Not.Null);
            Assert.That(previewRoot, Is.Not.Null);

            preview.enabled = true;
            preview.Show(prefab, Cells((3, 3)));
            UnityEngine.Object.Destroy(owner);
            yield return null;
            Assert.That(previewRoot.transform.childCount, Is.Zero);
            Assert.That(prefab, Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator Grid_CreatesExactBasePoolAndReusableOutsideCapableFootprintPool()
        {
            var gridRoot = CreateRoot("GridPoolRoot");
            var owner = CreateRoot("GridPoolOwner");
            var grid = owner.AddComponent<GridHighlightView>();
            var settings = new GridSettings(1.25f);
            var space = new DecorationGridSpace(
                settings,
                new LayoutBounds(new GridPosition(10, -3), new GridSize(8, 8)));
            grid.Configure(gridRoot.transform, space, worldMaterial, theme);
            grid.ShowGrid(settings);
            yield return null;

            var baseCells = DirectChildrenNamed(gridRoot.transform, "BaseCell");
            Assert.That(baseCells.Count, Is.EqualTo(64));
            Assert.That(
                gridRoot.GetComponentsInChildren<Collider>(true),
                Is.Empty);
            Assert.That(
                baseCells.Select(cell => cell.GetComponent<Renderer>()),
                Has.All.Matches<Renderer>(renderer =>
                    renderer.enabled && renderer.gameObject.activeInHierarchy));
            var expectedCenters = new List<Vector3>(64);
            for (var x = 0; x < 8; x++)
            {
                for (var y = 0; y < 8; y++)
                {
                    expectedCenters.Add(new Vector3(
                        (x + 0.5f) * 1.25f,
                        0f,
                        (y + 0.5f) * 1.25f));
                }
            }
            Assert.That(
                baseCells.Select(cell => cell.localPosition),
                Is.EquivalentTo(expectedCenters),
                "All 64 unique base-cell centers must match the literal 8x8 lattice.");

            var footprints = new[]
            {
                Cells((10, -3)),
                Cells((10, -3), (10, -2)),
                Cells((10, -3), (10, -2), (10, -1)),
                Cells((9, -4), (9, -3), (9, -2), (10, -4), (10, -3), (10, -2))
            };

            foreach (var footprint in footprints)
            {
                grid.ShowFootprint(footprint, valid: true);
                var activeFootprint = DirectChildrenNamed(gridRoot.transform, "FootprintCell")
                    .Where(child => child.gameObject.activeSelf)
                    .ToArray();
                Assert.That(activeFootprint, Has.Length.EqualTo(footprint.Count));
                var expectedFootprintCenters = footprint.Select(cell => new Vector3(
                    (cell.X - 10 + 0.5f) * 1.25f,
                    GridHighlightView.FootprintHeight,
                    (cell.Y + 3 + 0.5f) * 1.25f));
                Assert.That(
                    activeFootprint.Select(child => child.localPosition),
                    Is.EquivalentTo(expectedFootprintCenters));
                Assert.That(
                    activeFootprint.Select(child => child.Find("Fill").GetComponent<Renderer>()),
                    Has.All.Matches<Renderer>(renderer =>
                        renderer.enabled && renderer.gameObject.activeInHierarchy));
                foreach (var activeCell in activeFootprint)
                {
                    var markRenderers = activeCell.Find("GeometryMark")
                        .GetComponentsInChildren<Renderer>(false);
                    Assert.That(
                        markRenderers,
                        Is.Not.Empty,
                        $"{activeCell.name} must expose its own visible geometry mark.");
                    Assert.That(
                        markRenderers,
                        Has.All.Matches<Renderer>(renderer =>
                            renderer.enabled && renderer.gameObject.activeInHierarchy));
                }
            }

            Assert.That(
                DirectChildrenNamed(gridRoot.transform, "FootprintCell").Count,
                Is.EqualTo(6),
                "The footprint pool must reuse its high-water mark instead of growing per call.");
            AssertVector(
                DirectChildrenNamed(gridRoot.transform, "FootprintCell")[0].localPosition,
                space.GetCellCenterLocal(new GridPosition(9, -4), GridHighlightView.FootprintHeight));

            grid.ShowFootprint(Cells((10, -3)), valid: true);
            Assert.That(
                DirectChildrenNamed(gridRoot.transform, "FootprintCell").Count,
                Is.EqualTo(6));
        }

        [UnityTest]
        public IEnumerator Grid_ValidityUsesThemePropertyBlocksAndDifferentGeometryMarks()
        {
            var gridRoot = CreateRoot("GridValidityRoot");
            var grid = CreateRoot("GridValidityOwner").AddComponent<GridHighlightView>();
            var space = CreateGridSpace();
            grid.Configure(gridRoot.transform, space, worldMaterial, theme);
            grid.ShowGrid(space.Settings);
            var sharedColorBefore = ReadMaterialColor(worldMaterial);
            var cells = Cells((1, 1), (1, 2), (1, 3));

            grid.ShowFootprint(cells, valid: true);
            yield return null;
            var footprintCell = DirectChildrenNamed(gridRoot.transform, "FootprintCell")[0];
            var fill = footprintCell.Find("Fill").GetComponent<Renderer>();
            var markRoot = footprintCell.Find("GeometryMark");
            var validRenderers = markRoot.GetComponentsInChildren<Renderer>(false);
            AssertRendererColor(fill, theme.Colors.Accent);
            Assert.That(validRenderers, Has.Length.EqualTo(1));
            Assert.That(validRenderers[0].name, Is.EqualTo("ValidDiamond"));

            grid.ShowFootprint(cells, valid: false);
            AssertRendererColor(fill, theme.Colors.Destructive);
            var invalidRenderers = markRoot.GetComponentsInChildren<Renderer>(false);
            Assert.That(invalidRenderers, Has.Length.EqualTo(2));
            Assert.That(
                invalidRenderers.Select(renderer => renderer.name),
                Is.EquivalentTo(new[] { "InvalidBarA", "InvalidBarB" }));
            Assert.That(ReadMaterialColor(worldMaterial), Is.EqualTo(sharedColorBefore));
            Assert.That(gridRoot.GetComponentsInChildren<Collider>(true), Is.Empty);
        }

        [UnityTest]
        public IEnumerator Grid_ClearHideDisableAndReenableReusePoolsWithoutStaleVisuals()
        {
            var gridRoot = CreateRoot("GridLifecycleRoot");
            var owner = CreateRoot("GridLifecycleOwner");
            var grid = owner.AddComponent<GridHighlightView>();
            var space = CreateGridSpace();
            grid.Configure(gridRoot.transform, space, worldMaterial, theme);
            grid.ShowGrid(space.Settings);
            grid.ShowFootprint(
                Cells((1, 1), (1, 2), (1, 3), (2, 1), (2, 2), (2, 3)),
                valid: true);
            yield return null;
            var basePoolCount = DirectChildrenNamed(gridRoot.transform, "BaseCell").Count;
            var footprintPoolCount = DirectChildrenNamed(gridRoot.transform, "FootprintCell").Count;

            grid.ClearFootprint();
            Assert.That(ActiveNamedChildren(gridRoot.transform, "FootprintCell"), Is.Zero);
            grid.HideGrid();
            Assert.That(ActiveNamedChildren(gridRoot.transform, "BaseCell"), Is.Zero);

            grid.ShowGrid(space.Settings);
            grid.ShowFootprint(Cells((-1, -1), (0, -1)), valid: false);
            grid.enabled = false;
            yield return null;
            Assert.That(ActiveNamedChildren(gridRoot.transform, "BaseCell"), Is.Zero);
            Assert.That(ActiveNamedChildren(gridRoot.transform, "FootprintCell"), Is.Zero);

            grid.enabled = true;
            grid.ShowGrid(space.Settings);
            grid.ShowFootprint(Cells((2, 2)), valid: true);
            Assert.That(DirectChildrenNamed(gridRoot.transform, "BaseCell").Count, Is.EqualTo(basePoolCount));
            Assert.That(DirectChildrenNamed(gridRoot.transform, "FootprintCell").Count, Is.EqualTo(footprintPoolCount));
            Assert.That(ActiveNamedChildren(gridRoot.transform, "BaseCell"), Is.EqualTo(64));
            Assert.That(ActiveNamedChildren(gridRoot.transform, "FootprintCell"), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator VisualCheckpoint_CapturesValidAndInvalidPortraitFootprints()
        {
            var gridRoot = CreateRoot("VisualGridRoot");
            var previewRoot = CreateRoot("VisualPreviewRoot");
            var grid = CreateRoot("VisualGridOwner").AddComponent<GridHighlightView>();
            var preview = CreateRoot("VisualPreviewOwner").AddComponent<FurniturePreviewView>();
            var space = CreateGridSpace();
            grid.Configure(gridRoot.transform, space, worldMaterial, theme);
            preview.Configure(previewRoot.transform, space, theme);
            grid.ShowGrid(space.Settings);

            var cameraObject = CreateRoot("Task4PortraitCamera");
            var camera = cameraObject.AddComponent<UnityEngine.Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.07f, 0.06f, 1f);
            camera.orthographic = true;
            camera.orthographicSize = 10.5f;
            camera.aspect = 9f / 16f;
            camera.transform.position = new Vector3(9f, 11f, -8f);
            camera.transform.LookAt(new Vector3(4f, 0f, 4f));

            var lightObject = CreateRoot("Task4PortraitLight");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.transform.rotation = Quaternion.Euler(45f, -35f, 0f);

            var outputDirectory = Path.Combine(
                Directory.GetParent(Application.dataPath).FullName,
                "outputs",
                "phase6-task4-visual");
            Directory.CreateDirectory(outputDirectory);

            var cases = new[]
            {
                new VisualCase("1x1", 1, 1, new GridPosition(2, 2), new GridPosition(-1, 2)),
                new VisualCase("1x3", 1, 3, new GridPosition(3, 2), new GridPosition(-1, 2)),
                new VisualCase("2x3", 2, 3, new GridPosition(3, 2), new GridPosition(7, 2))
            };

            foreach (var visualCase in cases)
            {
                var prefab = CreateFurniturePrefab(
                    "VisualCounter" + visualCase.Label,
                    visualCase.Width,
                    visualCase.Depth);

                var validCells = RectangleCells(
                    visualCase.ValidAnchor,
                    visualCase.Width,
                    visualCase.Depth);
                preview.Show(prefab, validCells);
                preview.SetPlacement(validCells, FurnitureRotation.Degrees0, 0.9f);
                preview.SetValidity(true);
                grid.ShowFootprint(validCells, valid: true);
                CapturePortrait(
                    camera,
                    Path.Combine(outputDirectory, $"valid-{visualCase.Label}.png"));

                var invalidCells = RectangleCells(
                    visualCase.InvalidAnchor,
                    visualCase.Width,
                    visualCase.Depth);
                preview.SetPlacement(invalidCells, FurnitureRotation.Degrees0, 0.9f);
                preview.SetValidity(false);
                grid.ShowFootprint(invalidCells, valid: false);
                CapturePortrait(
                    camera,
                    Path.Combine(outputDirectory, $"invalid-{visualCase.Label}.png"));
            }

            foreach (var visualCase in cases)
            {
                var validPath = Path.Combine(outputDirectory, $"valid-{visualCase.Label}.png");
                var invalidPath = Path.Combine(outputDirectory, $"invalid-{visualCase.Label}.png");
                AssertPortraitEvidence(validPath, camera.backgroundColor);
                AssertPortraitEvidence(invalidPath, camera.backgroundColor);
                Assert.That(
                    File.ReadAllBytes(validPath).SequenceEqual(File.ReadAllBytes(invalidPath)),
                    Is.False,
                    $"Valid and invalid {visualCase.Label} captures must not be byte-identical.");
            }

            yield return null;
        }

        [Test]
        public void Preview_TryGetWorldBoundsAggregatesActiveRenderersAndClearsOnHide()
        {
            var previewRoot = CreateRoot("BoundsPreviewRoot");
            var preview = CreateRoot("BoundsPreviewOwner").AddComponent<FurniturePreviewView>();
            var space = CreateGridSpace();
            preview.Configure(previewRoot.transform, space, theme);
            var prefab = CreateFurniturePrefab("BoundsCounter", 2, 1);
            var cells = RectangleCells(new GridPosition(2, 2), 2, 1);

            Assert.That(preview.TryGetWorldBounds(out _), Is.False);
            preview.Show(prefab, cells);
            preview.SetPlacement(cells, FurnitureRotation.Degrees0, 0.35f);

            Assert.That(preview.TryGetWorldBounds(out var bounds), Is.True);
            Assert.That(bounds.size.x, Is.GreaterThan(0f));
            Assert.That(bounds.size.y, Is.GreaterThan(0f));
            Assert.That(bounds.size.z, Is.GreaterThan(0f));
            Assert.That(bounds.Contains(bounds.center), Is.True);

            preview.Hide();
            Assert.That(preview.TryGetWorldBounds(out _), Is.False);
        }

        private static DecorationGridSpace CreateGridSpace()
        {
            return CreateGridSpace(
                new GridPosition(0, 0),
                new GridSize(8, 8),
                1f);
        }

        private static DecorationGridSpace CreateGridSpace(
            GridPosition origin,
            GridSize size,
            float cellSize)
        {
            return new DecorationGridSpace(
                new GridSettings(cellSize),
                new LayoutBounds(origin, size));
        }

        private Material CreateWorldMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ??
                         Shader.Find("Unlit/Color") ??
                         Shader.Find("Hidden/InternalErrorShader");
            Assert.That(shader, Is.Not.Null, "A runtime world shader is required by the fixture.");
            var material = new Material(shader)
            {
                name = "Task4RuntimeWorldMaterial"
            };

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", Color.white);
            }
            else if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", Color.white);
            }

            ownedAssets.Add(material);
            return material;
        }

        private GameObject CreateFurniturePrefab(string name, int width, int depth)
        {
            var root = CreateRoot(name);
            for (var x = 0; x < width; x++)
            {
                for (var y = 0; y < depth; y++)
                {
                    var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    visual.name = $"Visual_{x}_{y}";
                    visual.transform.SetParent(root.transform, false);
                    visual.transform.localPosition = new Vector3(
                        x - (width - 1) * 0.5f,
                        0.35f,
                        y - (depth - 1) * 0.5f);
                    visual.transform.localScale = new Vector3(0.84f, 0.7f, 0.84f);
                    visual.GetComponent<Renderer>().sharedMaterial = worldMaterial;
                    if (x == 0 && y == 0)
                    {
                        visual.AddComponent<Task4Selectable>();
                    }
                }
            }

            root.AddComponent<Task4Selectable>();
            root.SetActive(false);
            return root;
        }

        private FurnitureDefinitionAsset CreateDefinition(
            string definitionId,
            int width,
            int depth,
            GameObject prefab)
        {
            var asset = ScriptableObject.CreateInstance<FurnitureDefinitionAsset>();
            asset.name = definitionId;
            SetField(asset, "definitionId", definitionId);
            SetField(asset, "displayName", definitionId);
            SetField(asset, "footprintWidth", width);
            SetField(asset, "footprintDepth", depth);
            SetField(asset, "allowedPlacementSurfaces", PlacementSurfaceType.Floor);
            SetField(asset, "functionType", FurnitureFunctionType.None);
            SetField(asset, "prefab", prefab);
            ownedAssets.Add(asset);
            return asset;
        }

        private FurnitureContentCatalog CreateContentCatalog(
            params FurnitureDefinitionAsset[] definitions)
        {
            var catalog = ScriptableObject.CreateInstance<FurnitureContentCatalog>();
            catalog.name = "Task4RuntimeCatalog";
            SetField(catalog, "entries", definitions.ToList());
            ownedAssets.Add(catalog);
            return catalog;
        }

        private GameObject CreateRoot(string name)
        {
            var root = new GameObject(name);
            ownedRoots.Add(root);
            return root;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field '{fieldName}'.");
            field.SetValue(target, value);
        }

        private static IReadOnlyList<GridPosition> Cells(
            params (int x, int y)[] coordinates)
        {
            return coordinates
                .Select(coordinate => new GridPosition(coordinate.x, coordinate.y))
                .ToArray();
        }

        private static IReadOnlyList<GridPosition> RectangleCells(
            GridPosition anchor,
            int width,
            int depth)
        {
            var cells = new List<GridPosition>(width * depth);
            for (var x = 0; x < width; x++)
            {
                for (var y = 0; y < depth; y++)
                {
                    cells.Add(new GridPosition(anchor.X + x, anchor.Y + y));
                }
            }

            return cells;
        }

        private static void AssertVector(Vector3 actual, Vector3 expected)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(Epsilon));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(Epsilon));
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(Epsilon));
        }

        private static void AssertIssue(
            IReadOnlyList<FurnitureSceneIssue> issues,
            FurnitureSceneIssueCode expectedCode,
            string expectedLogCode,
            string expectedInstanceId,
            string expectedDefinitionId)
        {
            var matches = issues.Where(issue => issue.Code == expectedCode).ToArray();
            Assert.That(matches, Has.Length.EqualTo(1));
            Assert.That(matches[0].LogCode, Is.EqualTo(expectedLogCode));
            Assert.That(matches[0].InstanceId, Is.EqualTo(expectedInstanceId));
            Assert.That(matches[0].DefinitionId, Is.EqualTo(expectedDefinitionId));
            Assert.That(matches[0].Message, Is.Not.Empty);
        }

        private static Transform ActiveChild(Transform parent)
        {
            return parent.Cast<Transform>().Single(child => child.gameObject.activeSelf);
        }

        private static int ActiveDirectChildren(Transform parent)
        {
            return parent.Cast<Transform>().Count(child => child.gameObject.activeSelf);
        }

        private static List<Transform> DirectChildrenNamed(Transform parent, string prefix)
        {
            return parent.Cast<Transform>()
                .Where(child => child.name.StartsWith(prefix, StringComparison.Ordinal))
                .OrderBy(child => child.GetSiblingIndex())
                .ToList();
        }

        private static int ActiveNamedChildren(Transform parent, string prefix)
        {
            return DirectChildrenNamed(parent, prefix)
                .Count(child => child.gameObject.activeSelf);
        }

        private static Color ReadMaterialColor(Material material)
        {
            if (material.HasProperty("_BaseColor"))
            {
                return material.GetColor("_BaseColor");
            }

            return material.HasProperty("_Color")
                ? material.GetColor("_Color")
                : Color.white;
        }

        private static void AssertRendererColor(Renderer renderer, Color expected)
        {
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            var propertyId = renderer.sharedMaterial != null &&
                             renderer.sharedMaterial.HasProperty("_BaseColor")
                ? Shader.PropertyToID("_BaseColor")
                : Shader.PropertyToID("_Color");
            var actual = block.GetColor(propertyId);
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(Epsilon));
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(Epsilon));
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(Epsilon));
            Assert.That(actual.a, Is.EqualTo(expected.a).Within(Epsilon));
        }

        private static void CapturePortrait(UnityEngine.Camera camera, string path)
        {
            var renderTexture = new RenderTexture(540, 960, 24, RenderTextureFormat.ARGB32);
            var texture = new Texture2D(540, 960, TextureFormat.RGBA32, false);
            var previousActive = RenderTexture.active;

            try
            {
                camera.targetTexture = renderTexture;
                camera.Render();
                RenderTexture.active = renderTexture;
                texture.ReadPixels(new Rect(0f, 0f, 540f, 960f), 0, 0);
                texture.Apply();
                var bytes = texture.EncodeToPNG();
                Assert.That(bytes.Length, Is.GreaterThan(1000));
                File.WriteAllBytes(path, bytes);
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previousActive;
                renderTexture.Release();
                UnityEngine.Object.Destroy(renderTexture);
                UnityEngine.Object.Destroy(texture);
            }
        }

        private static void AssertPortraitEvidence(string path, Color background)
        {
            Assert.That(File.Exists(path), Is.True, path);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.That(texture.LoadImage(File.ReadAllBytes(path)), Is.True);
                Assert.That(texture.width, Is.EqualTo(540));
                Assert.That(texture.height, Is.EqualTo(960));
                var visiblePixels = texture.GetPixels().Count(pixel =>
                    Mathf.Abs(pixel.r - background.r) > 0.02f ||
                    Mathf.Abs(pixel.g - background.g) > 0.02f ||
                    Mathf.Abs(pixel.b - background.b) > 0.02f);
                Assert.That(visiblePixels, Is.GreaterThan(1000));
            }
            finally
            {
                UnityEngine.Object.Destroy(texture);
            }
        }

        private static Vector2 BeginFurnitureEdgePan(
            Task7ControllerFixture fixture,
            int touchId)
        {
            fixture.CameraDriver.EdgeZonePixels = fixture.Camera.pixelRect.width;
            fixture.CameraDriver.MaxEdgeSpeedPixelsPerSecond = 1f;
            var start = fixture.ScreenForCell(
                fixture.Session.ActivePreview.ProposedPosition);
            var dragPosition = start + Vector2.right
                * (fixture.CameraSettings.DragThresholdPixels + 1f);
            fixture.SendTouch(touchId, start, Vector2.zero,
                UnityEngine.InputSystem.TouchPhase.Began);
            fixture.SendTouch(touchId, dragPosition, dragPosition - start,
                UnityEngine.InputSystem.TouchPhase.Moved);
            Assert.That(fixture.CameraDriver.IsEdgeAutoPanning, Is.True,
                "The fixture must establish active fake-touch edge auto-pan.");
            return dragPosition;
        }

        private BootstrapFixture CreateBootstrapFixture()
        {
            var counterPrefab = CreateFurniturePrefab("PF_Task7InitialCounter", 1, 1);
            var counter = CreateDefinition(
                "furniture.counter.module.01",
                1,
                1,
                counterPrefab);
            var catalog = CreateContentCatalog(counter);
            var entranceObject = CreateRoot("Task7EntrancePortal");
            var entrance = entranceObject.AddComponent<EntrancePortalAuthoring>();
            SetField(entrance, "entranceId", "entrance.main");
            SetField(entrance, "originX", 3);
            SetField(entrance, "originY", 0);
            var runtimeObject = CreateRoot("Task7CafeLayoutRuntime");
            var runtime = runtimeObject.AddComponent<CafeLayoutRuntime>();
            SetField(runtime, "contentCatalog", catalog);
            SetField(runtime, "entrancePortal", entrance);
            return new BootstrapFixture(
                runtime,
                catalog,
                entrance,
                counter,
                counterPrefab);
        }

        private Task7ControllerFixture CreateControllerFixture(
            float cellSize = 1f,
            bool addSecondFurniture = false,
            GameSpeed initialSpeed = GameSpeed.Normal)
        {
            return new Task7ControllerFixture(
                worldMaterial,
                theme,
                cellSize,
                addSecondFurniture,
                initialSpeed: initialSpeed);
        }

        private Task7ControllerFixture CreateStartupControllerFixture(
            string omittedDependency = null,
            bool useMismatchedControllerCatalog = false,
            bool deactivateFormalRoot = false)
        {
            return new Task7ControllerFixture(
                worldMaterial,
                theme,
                1f,
                addSecondFurniture: false,
                deferBootstrapToController: true,
                omittedStartupDependency: omittedDependency,
                useMismatchedControllerCatalog: useMismatchedControllerCatalog,
                deactivateFormalRoot: deactivateFormalRoot);
        }

        private static T GetField<T>(object target, string fieldName)
        {
            var field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field '{fieldName}'.");
            return (T)field.GetValue(target);
        }

        private static object InvokePrivate(
            object target,
            string methodName,
            params object[] arguments)
        {
            var method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"Missing method '{methodName}'.");
            return method.Invoke(target, arguments);
        }

        private sealed class BootstrapFixture
        {
            public BootstrapFixture(
                CafeLayoutRuntime runtime,
                FurnitureContentCatalog catalog,
                EntrancePortalAuthoring entrance,
                FurnitureDefinitionAsset counterDefinition,
                GameObject counterPrefab)
            {
                Runtime = runtime;
                Catalog = catalog;
                Entrance = entrance;
                CounterDefinition = counterDefinition;
                CounterPrefab = counterPrefab;
            }

            public CafeLayoutRuntime Runtime { get; }
            public FurnitureContentCatalog Catalog { get; }
            public EntrancePortalAuthoring Entrance { get; }
            public FurnitureDefinitionAsset CounterDefinition { get; }
            public GameObject CounterPrefab { get; }
        }

        private sealed class Task7ControllerFixture : IDisposable
        {
            private readonly List<GameObject> roots = new List<GameObject>();
            private readonly List<UnityEngine.Object> assets = new List<UnityEngine.Object>();
            private readonly List<EventSystem> disabledEventSystems = new List<EventSystem>();
            private readonly List<GraphicRaycaster> disabledRaycasters = new List<GraphicRaycaster>();
            private readonly Material sharedWorldMaterial;
            private readonly AnimalCafeUiTheme sharedTheme;
            private readonly Transform originalCatalogueContent;
            private GameObject uiOverlay;
            private int touchFrame;

            public Task7ControllerFixture(
                Material worldMaterial,
                AnimalCafeUiTheme theme,
                float cellSize,
                bool addSecondFurniture,
                bool deferBootstrapToController = false,
                string omittedStartupDependency = null,
                bool useMismatchedControllerCatalog = false,
                bool deactivateFormalRoot = false,
                bool failAfterDisablingUiSystems = false,
                GameSpeed initialSpeed = GameSpeed.Normal)
            {
                sharedWorldMaterial = worldMaterial;
                sharedTheme = theme;
                try
                {
                    DisableExistingUiSystems();
                    if (failAfterDisablingUiSystems)
                    {
                        throw new InvalidOperationException(
                            "Intentional fixture-construction failure.");
                    }

                var modulePrefab = CreateFurniturePrefab("PF_Task7Module", 1, 1);
                var oneByTwoPrefab = CreateFurniturePrefab("PF_Task7OneByTwo", 1, 2);
                var oneByThreePrefab = CreateFurniturePrefab("PF_Task7OneByThree", 1, 3);
                var twoByThreePrefab = CreateFurniturePrefab("PF_Task7TwoByThree", 2, 3);
                var module = CreateDefinition(
                    "furniture.counter.module.01", "Counter Module", 1, 1, modulePrefab);
                var oneByTwo = CreateDefinition(
                    "furniture.counter.preset.1x2", "Counter 1 x 2", 1, 2, oneByTwoPrefab);
                var oneByThree = CreateDefinition(
                    "furniture.counter.preset.1x3", "Counter 1 x 3", 1, 3, oneByThreePrefab);
                var twoByThree = CreateDefinition(
                    "furniture.counter.preset.2x3", "Counter 2 x 3", 2, 3, twoByThreePrefab);
                Definitions = new[] { module, oneByTwo, oneByThree, twoByThree };

                Catalog = ScriptableObject.CreateInstance<FurnitureContentCatalog>();
                Catalog.name = "FC_Task7Production";
                SetField(Catalog, "entries", Definitions.ToList());
                assets.Add(Catalog);

                var entranceObject = Root("Task7Entrance");
                Entrance = entranceObject.AddComponent<EntrancePortalAuthoring>();
                SetField(Entrance, "entranceId", "entrance.main");
                SetField(Entrance, "originX", 3);
                SetField(Entrance, "originY", 0);

                var runtimeObject = Root("Task7LayoutRuntime");
                LayoutRuntime = runtimeObject.AddComponent<CafeLayoutRuntime>();
                SetField(LayoutRuntime, "contentCatalog", Catalog);
                SetField(LayoutRuntime, "entrancePortal", Entrance);
                if (!deferBootstrapToController)
                {
                    LayoutRuntime.Initialize();
                    if (addSecondFurniture)
                    {
                        Assert.That(LayoutRuntime.Layout.PlaceFurniture(FurnitureInstance.Restore(
                            "00000000000000000000000000000002",
                            "furniture.counter.module.01",
                            new GridPosition(6, 5),
                            FurnitureRotation.Degrees0)).Succeeded, Is.True);
                    }
                }

                GridSpace = deferBootstrapToController
                    ? default
                    : new DecorationGridSpace(
                        new GridSettings(cellSize),
                        new LayoutBounds(new GridPosition(0, 0), new GridSize(8, 8)));
                GridRoot = Root("Task7GridSouthwestRoot").transform;
                var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.name = "Task7ConfiguredFloor";
                floor.transform.SetParent(GridRoot, false);
                floor.transform.localPosition = new Vector3(4f, -0.1f, 4f);
                floor.transform.localScale = new Vector3(8f, 0.2f, 8f);
                FloorCollider = floor.GetComponent<Collider>();
                floor.GetComponent<Renderer>().sharedMaterial = sharedWorldMaterial;

                var cameraObject = Root("Task7Camera");
                Camera = cameraObject.AddComponent<UnityEngine.Camera>();
                Camera.orthographic = true;
                Camera.orthographicSize = 6f;
                Camera.nearClipPlane = 0.01f;
                Camera.farClipPlane = 100f;
                Camera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                SetCameraFloorPoint(GridRoot.TransformPoint(new Vector3(4f, 0f, 4f)));
                CameraSettings = ScriptableObject.CreateInstance<CameraSettings>();
                CameraSettings.name = "Task7CameraSettings";
                CameraSettings.PanSpeed = 0.02f;
                CameraSettings.ZoomSpeed = 0.02f;
                CameraSettings.PositionMin = new Vector2(-100f, -100f);
                CameraSettings.PositionMax = new Vector2(100f, 100f);
                CameraSettings.MinOrthographicSize = 1f;
                CameraSettings.MaxOrthographicSize = 20f;
                CameraSettings.DragThresholdPixels = 6f;
                assets.Add(CameraSettings);
                LegacyInput = cameraObject.AddComponent<Task7QueuedCameraInput>();
                CameraController = cameraObject.AddComponent<CafeCameraController>();
                CameraController.Configure(Camera, CameraSettings, LegacyInput);

                var interactionObject = Root("Task7SceneInteraction");
                SceneInteraction = interactionObject.AddComponent<SceneInteractionController>();
                SceneInteraction.Configure(Camera, LegacyInput, new UiPointerBoundary());

                FormalRoot = Root("Task7FormalRoot").transform;
                PreviewRoot = Root("Task7PreviewRoot").transform;
                GridVisualRoot = Root("Task7GridVisualRoot").transform;
                Registry = Root("Task7Registry").AddComponent<FurnitureSceneRegistry>();
                Preview = Root("Task7PreviewView").AddComponent<FurniturePreviewView>();
                Grid = Root("Task7GridView").AddComponent<GridHighlightView>();
                if (!deferBootstrapToController)
                {
                    var runtimeGridSpace = new DecorationGridSpace(
                        LayoutRuntime.Layout.GridSettings,
                        new LayoutBounds(new GridPosition(0, 0), new GridSize(8, 8)));
                    Registry.Configure(Catalog, FormalRoot, runtimeGridSpace);
                    Registry.Rebuild(LayoutRuntime.Layout.FurnitureInstances);
                    Preview.Configure(PreviewRoot, runtimeGridSpace, sharedTheme);
                    Grid.Configure(
                        GridVisualRoot,
                        runtimeGridSpace,
                        sharedWorldMaterial,
                        sharedTheme);
                }
                else
                {
                    FormalRoot.SetParent(GridRoot, false);
                    PreviewRoot.SetParent(GridRoot, false);
                    GridVisualRoot.SetParent(GridRoot, false);
                }
                CameraDriver = Root("Task7CameraDriver").AddComponent<DecorationCameraDriver>();
                CameraDriver.Configure(CameraController);

                EventSystemObject = Root("Task7EventSystem");
                EventSystemObject.AddComponent<EventSystem>();
                CanvasRoot = new GameObject(
                    "Task7Canvas",
                    typeof(RectTransform),
                    typeof(Canvas),
                    typeof(GraphicRaycaster));
                roots.Add(CanvasRoot);
                CanvasRoot.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                Catalogue = new CatalogueFixture(CanvasRoot.transform, Definitions, assets);
                Action = new ActionFixture(CanvasRoot.transform);
                Modal = new ModalFixture(CanvasRoot.transform);
                HudButton = UiButton("DecorationModeButton", CanvasRoot.transform);
                HudLabel = UiObject("DecorationModeButtonLabel", HudButton.transform)
                    .AddComponent<TextMeshProUGUI>();
                var timeControlRoot = UiObject("Task7TimeControls", CanvasRoot.transform);
                TimeControls = timeControlRoot.AddComponent<TimeControlPanel>();
                PauseTimeButton = UiButton("PauseButton", timeControlRoot.transform);
                NormalTimeButton = UiButton("NormalButton", timeControlRoot.transform);
                FastTimeButton = UiButton("FastButton", timeControlRoot.transform);
                SetField(TimeControls, "pauseButton", PauseTimeButton);
                SetField(TimeControls, "normalButton", NormalTimeButton);
                SetField(TimeControls, "fastButton", FastTimeButton);
                TimeControls.enabled = false;
                originalCatalogueContent = GetField<Transform>(Catalogue.View, "contentRoot");

                CatalogueAsset = ScriptableObject.CreateInstance<DecorationCatalogueAsset>();
                var entries = new List<DecorationCatalogueEntry>();
                foreach (var definition in Definitions)
                {
                    var entry = new DecorationCatalogueEntry();
                    SetField(entry, "definition", definition);
                    SetField(entry, "thumbnail", CreateSprite());
                    entries.Add(entry);
                }
                SetField(CatalogueAsset, "entries", entries);
                assets.Add(CatalogueAsset);

                var controllerCatalog = Catalog;
                if (useMismatchedControllerCatalog)
                {
                    controllerCatalog = ScriptableObject.CreateInstance<FurnitureContentCatalog>();
                    controllerCatalog.name = "FC_Task7MiswiredController";
                    SetField(controllerCatalog, "entries", Definitions.ToList());
                    assets.Add(controllerCatalog);
                }

                Time = new Task7FakeGameTimeService(initialSpeed);
                Touch = new Task7FakeTouchSource();
                Mouse = new Task7FakeMouseSource();
                var controllerObject = Root("Task7DecorationController");
                controllerObject.SetActive(false);
                Controller = controllerObject.AddComponent<DecorationModeController>();
                SetField(Controller, "layoutRuntime",
                    omittedStartupDependency == "layoutRuntime" ? null : LayoutRuntime);
                SetField(Controller, "contentCatalog",
                    omittedStartupDependency == "contentCatalog" ? null : controllerCatalog);
                SetField(Controller, "catalogueAsset",
                    omittedStartupDependency == "catalogueAsset" ? null : CatalogueAsset);
                SetField(Controller, "targetCamera",
                    omittedStartupDependency == "targetCamera" ? null : Camera);
                SetField(Controller, "cameraSettings",
                    omittedStartupDependency == "cameraSettings" ? null : CameraSettings);
                SetField(Controller, "cameraController",
                    omittedStartupDependency == "cameraController" ? null : CameraController);
                SetField(Controller, "sceneInteraction",
                    omittedStartupDependency == "sceneInteraction" ? null : SceneInteraction);
                SetField(Controller, "floorCollider",
                    omittedStartupDependency == "floorCollider" ? null : FloorCollider);
                SetField(Controller, "gridRoot",
                    omittedStartupDependency == "gridRoot" ? null : GridRoot);
                SetField(Controller, "sceneRegistry",
                    omittedStartupDependency == "sceneRegistry" ? null : Registry);
                SetField(Controller, "previewView",
                    omittedStartupDependency == "previewView" ? null : Preview);
                SetField(Controller, "gridView",
                    omittedStartupDependency == "gridView" ? null : Grid);
                SetField(Controller, "cameraDriver",
                    omittedStartupDependency == "cameraDriver" ? null : CameraDriver);
                SetField(Controller, "catalogueView",
                    omittedStartupDependency == "catalogueView" ? null : Catalogue.View);
                SetField(Controller, "actionBarView",
                    omittedStartupDependency == "actionBarView" ? null : Action.View);
                SetField(Controller, "storeModalView",
                    omittedStartupDependency == "storeModalView" ? null : Modal.View);
                SetField(Controller, "decorationModeButton",
                    omittedStartupDependency == "decorationModeButton" ? null : HudButton);
                SetField(Controller, "decorationModeButtonLabel",
                    omittedStartupDependency == "decorationModeButtonLabel" ? null : HudLabel);
                SetField(Controller, "timeControlPanel",
                    omittedStartupDependency == "timeControlPanel" ? null : TimeControls);
                if (deferBootstrapToController)
                {
                    SetField(Controller, "furnitureRepresentationRoot",
                        omittedStartupDependency == "furnitureRepresentationRoot"
                            ? null
                            : FormalRoot);
                    SetField(Controller, "furniturePreviewRoot",
                        omittedStartupDependency == "furniturePreviewRoot"
                            ? null
                            : PreviewRoot);
                    SetField(Controller, "gridVisualRoot",
                        omittedStartupDependency == "gridVisualRoot"
                            ? null
                            : GridVisualRoot);
                    SetField(Controller, "gridMaterialTemplate",
                        omittedStartupDependency == "gridMaterialTemplate"
                            ? null
                            : sharedWorldMaterial);
                    SetField(Controller, "uiTheme",
                        omittedStartupDependency == "uiTheme" ? null : sharedTheme);
                    if (deactivateFormalRoot)
                    {
                        FormalRoot.gameObject.SetActive(false);
                    }
                }
                SetField(Controller, "gameTimeServiceOverride",
                    omittedStartupDependency == "gameTime" ? null : Time);
                SetField(Controller, "touchSourceOverride",
                    omittedStartupDependency == "touchSource" ? null : Touch);
                SetField(Controller, "mouseSourceOverride",
                    omittedStartupDependency == "mouseSource" ? null : Mouse);
                if (!deferBootstrapToController)
                {
                    SetField(Controller, "gridSpace", GridSpace);
                }
                if (!deferBootstrapToController)
                {
                    SetField(Controller, "runtimeBootstrapComplete", true);
                }
                controllerObject.SetActive(true);
                if (deferBootstrapToController
                    && GetField<bool>(Controller, "runtimeBootstrapComplete"))
                {
                    GridSpace = GetField<DecorationGridSpace>(Controller, "gridSpace");
                }
                Physics.SyncTransforms();
                }
                catch
                {
                    Dispose();
                    throw;
                }
            }

            public CafeLayoutRuntime LayoutRuntime { get; }
            public CafeLayout Layout => LayoutRuntime.Layout;
            public FurnitureContentCatalog Catalog { get; }
            public FurnitureDefinitionAsset[] Definitions { get; }
            public EntrancePortalAuthoring Entrance { get; }
            public DecorationCatalogueAsset CatalogueAsset { get; }
            public DecorationModeController Controller { get; private set; }
            public DecorationSession Session => GetField<DecorationSession>(Controller, "session");
            public DecorationGridSpace GridSpace { get; }
            public Transform GridRoot { get; }
            public Collider FloorCollider { get; }
            public UnityEngine.Camera Camera { get; }
            public CameraSettings CameraSettings { get; }
            public CafeCameraController CameraController { get; }
            public Task7QueuedCameraInput LegacyInput { get; }
            public SceneInteractionController SceneInteraction { get; }
            public Transform FormalRoot { get; }
            public Transform PreviewRoot { get; }
            public Transform GridVisualRoot { get; }
            public FurnitureSceneRegistry Registry { get; }
            public FurniturePreviewView Preview { get; }
            public GridHighlightView Grid { get; }
            public DecorationCameraDriver CameraDriver { get; }
            public CatalogueFixture Catalogue { get; }
            public ActionFixture Action { get; }
            public ModalFixture Modal { get; }
            public Button HudButton { get; }
            public TMP_Text HudLabel { get; }
            public TimeControlPanel TimeControls { get; }
            public Button PauseTimeButton { get; }
            public Button NormalTimeButton { get; }
            public Button FastTimeButton { get; }
            public Task7FakeGameTimeService Time { get; }
            public Task7FakeTouchSource Touch { get; }
            public Task7FakeMouseSource Mouse { get; }
            public GameObject EventSystemObject { get; }
            public GameObject CanvasRoot { get; }

            public int ActiveGridCellCount => GridVisualRoot.Cast<Transform>()
                .Count(child => child.name.StartsWith("BaseCell", StringComparison.Ordinal)
                    && child.gameObject.activeSelf);

            public int ActiveFootprintCellCount => GridVisualRoot.Cast<Transform>()
                .Count(child => child.name.StartsWith("FootprintCell", StringComparison.Ordinal)
                    && child.gameObject.activeSelf);

            public int ActivePreviewObjectCount => PreviewRoot.Cast<Transform>()
                .Count(child => child.gameObject.activeSelf);

            public void SetCameraFloorPoint(Vector3 point)
            {
                Camera.transform.position = point + Vector3.up * 10f;
            }

            public Vector2 ScreenForCell(GridPosition cell)
            {
                return Camera.WorldToScreenPoint(
                    GridRoot.TransformPoint(GridSpace.GetCellCenterLocal(cell)));
            }

            public bool TryProjectWorld(Vector3 world, out GridPosition position)
            {
                return TryProjectRay(new Ray(world + GridRoot.up * 10f, -GridRoot.up), out position);
            }

            public bool TryProjectRay(Ray ray, out GridPosition position)
            {
                var arguments = new object[] { ray, default(GridPosition) };
                var result = (bool)InvokePrivate(Controller, "TryRayToGrid", arguments);
                position = (GridPosition)arguments[1];
                return result;
            }

            public GridPosition ProjectScreen(Vector2 screen)
            {
                var arguments = new object[] { screen, default(GridPosition) };
                Assert.That((bool)InvokePrivate(
                    Controller, "TryProjectScreenToGrid", arguments), Is.True);
                return (GridPosition)arguments[1];
            }

            public DecorationTouchHit Classify(Vector2 screen)
            {
                return (DecorationTouchHit)InvokePrivate(
                    Controller,
                    "ClassifyPrimaryBegan",
                    screen);
            }

            public void SelectCatalogue(int index)
            {
                var tiles = Catalogue.Content
                    .GetComponentsInChildren<DecorationCatalogueTileView>(true)
                    .Where(tile => tile.gameObject.activeSelf
                        && tile.name.StartsWith("CatalogueTile_", StringComparison.Ordinal))
                    .OrderBy(tile => tile.name, StringComparer.Ordinal)
                    .ToArray();
                Assert.That(tiles.Length, Is.EqualTo(Definitions.Length));
                GetField<Button>(tiles[index], "button").onClick.Invoke();
            }

            public void SelectExisting(string instanceId)
            {
                InvokePrivate(Controller, "HandleFurnitureBegan", instanceId);
            }

            public void MovePreviewTo(GridPosition position)
            {
                InvokePrivate(Controller, "ApplyPreviewMove", position);
            }

            public void SendTouch(
                int touchId,
                Vector2 position,
                Vector2 delta,
                UnityEngine.InputSystem.TouchPhase phase)
            {
                SendTouches(new DecorationTouchPoint(touchId, position, delta, phase));
            }

            public void SendTouches(params DecorationTouchPoint[] touches)
            {
                Touch.Queue(++touchFrame, touches);
                InvokeControllerUpdate();
            }

            public void SendMouse(
                Vector2 position,
                Vector2 delta,
                UnityEngine.InputSystem.TouchPhase phase,
                bool active)
            {
                Mouse.Queue(
                    ++touchFrame,
                    new DecorationTouchPoint(
                        MouseDecorationInputSource.PointerId,
                        position,
                        delta,
                        phase),
                    active);
                InvokeControllerUpdate();
            }

            public void InvokeControllerUpdate()
            {
                if (Controller != null)
                {
                    InvokePrivate(Controller, "Update");
                }
            }

            public void SetFurnitureOffset(float value)
            {
                SetField(Controller, "furnitureDragOffsetPixels", value);
            }

            public void SetHoverHeight(float value)
            {
                SetField(Controller, "furnitureHoverHeight", value);
            }

            public void ShowUiOverlay(Vector2 screenPosition)
            {
                if (uiOverlay == null)
                {
                    uiOverlay = UiObject("Task7UiOverlay", CanvasRoot.transform);
                    var image = uiOverlay.AddComponent<Image>();
                    image.raycastTarget = true;
                    var rect = (RectTransform)uiOverlay.transform;
                    rect.sizeDelta = new Vector2(120f, 120f);
                }

                ((RectTransform)uiOverlay.transform).position = screenPosition;
                uiOverlay.SetActive(true);
                Canvas.ForceUpdateCanvases();
            }

            public void TapActionBar(Button button, int pointerId)
            {
                Assert.That(button, Is.Not.Null);
                var eventSystem = EventSystemObject.GetComponent<EventSystem>();
                Assert.That(eventSystem, Is.Not.Null);
                var eventData = new PointerEventData(eventSystem)
                {
                    pointerId = pointerId,
                    position = button.transform.position
                };

                Action.PointerHook.OnPointerDown(eventData);
                try
                {
                    button.onClick.Invoke();
                }
                finally
                {
                    Action.PointerHook.OnPointerUp(eventData);
                }
            }

            public void BreakCatalogueReferences()
            {
                SetField(Catalogue.View, "contentRoot", null);
            }

            public void RestoreCatalogueReferences()
            {
                SetField(Catalogue.View, "contentRoot", originalCatalogueContent);
            }

            public void AssertClosedAndClean()
            {
                Assert.That(Controller.IsOpen, Is.False);
                Assert.That(Controller.State, Is.EqualTo(DecorationSessionState.Closed));
                Assert.That(Catalogue.View.IsCatalogueVisible, Is.False);
                Assert.That(Action.View.IsVisible, Is.False);
                Assert.That(Modal.View.IsOpen, Is.False);
                Assert.That(ActivePreviewObjectCount, Is.Zero);
                Assert.That(ActiveGridCellCount, Is.Zero);
            }

            public void Track(GameObject gameObject)
            {
                roots.Add(gameObject);
            }

            public void Dispose()
            {
                if (Controller != null)
                {
                    Controller.ExitDecorationMode();
                }

                for (var index = roots.Count - 1; index >= 0; index--)
                {
                    if (roots[index] != null)
                    {
                        UnityEngine.Object.DestroyImmediate(roots[index]);
                    }
                }

                for (var index = assets.Count - 1; index >= 0; index--)
                {
                    if (assets[index] != null)
                    {
                        UnityEngine.Object.DestroyImmediate(assets[index]);
                    }
                }

                foreach (var raycaster in disabledRaycasters)
                {
                    if (raycaster != null)
                    {
                        raycaster.enabled = true;
                    }
                }

                foreach (var eventSystem in disabledEventSystems)
                {
                    if (eventSystem != null)
                    {
                        eventSystem.enabled = true;
                    }
                }
            }

            private void DisableExistingUiSystems()
            {
                foreach (var eventSystem in UnityEngine.Object.FindObjectsByType<EventSystem>(
                             FindObjectsInactive.Include,
                             FindObjectsSortMode.None))
                {
                    if (eventSystem.enabled)
                    {
                        eventSystem.enabled = false;
                        disabledEventSystems.Add(eventSystem);
                    }
                }

                foreach (var raycaster in UnityEngine.Object.FindObjectsByType<GraphicRaycaster>(
                             FindObjectsInactive.Include,
                             FindObjectsSortMode.None))
                {
                    if (raycaster.enabled)
                    {
                        raycaster.enabled = false;
                        disabledRaycasters.Add(raycaster);
                    }
                }
            }

            private GameObject Root(string name)
            {
                var root = new GameObject(name);
                roots.Add(root);
                return root;
            }

            private GameObject CreateFurniturePrefab(string name, int width, int depth)
            {
                var root = Root(name);
                for (var x = 0; x < width; x++)
                {
                    for (var y = 0; y < depth; y++)
                    {
                        var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        visual.name = $"Visual_{x}_{y}";
                        visual.transform.SetParent(root.transform, false);
                        visual.transform.localPosition = new Vector3(
                            x - (width - 1) * 0.5f,
                            0.35f,
                            y - (depth - 1) * 0.5f);
                        visual.transform.localScale = new Vector3(0.84f, 0.7f, 0.84f);
                        visual.GetComponent<Renderer>().sharedMaterial = sharedWorldMaterial;
                        if (x == 0 && y == 0)
                        {
                            visual.AddComponent<Task4Selectable>();
                        }
                    }
                }

                root.SetActive(false);
                return root;
            }

            private FurnitureDefinitionAsset CreateDefinition(
                string id,
                string displayName,
                int width,
                int depth,
                GameObject prefab)
            {
                var definition = ScriptableObject.CreateInstance<FurnitureDefinitionAsset>();
                definition.name = id;
                SetField(definition, "definitionId", id);
                SetField(definition, "displayName", displayName);
                SetField(definition, "footprintWidth", width);
                SetField(definition, "footprintDepth", depth);
                SetField(definition, "allowedPlacementSurfaces", PlacementSurfaceType.Floor);
                SetField(definition, "functionType", FurnitureFunctionType.None);
                SetField(definition, "prefab", prefab);
                assets.Add(definition);
                return definition;
            }

            private Sprite CreateSprite()
            {
                var texture = new Texture2D(2, 2);
                texture.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
                texture.Apply();
                var sprite = Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), Vector2.one * 0.5f);
                assets.Add(sprite);
                assets.Add(texture);
                return sprite;
            }

            private static GameObject UiObject(string name, Transform parent = null)
            {
                var result = new GameObject(name, typeof(RectTransform));
                if (parent != null)
                {
                    result.transform.SetParent(parent, false);
                }
                return result;
            }

            private static Button UiButton(string name, Transform parent)
            {
                var root = UiObject(name, parent);
                var image = root.AddComponent<Image>();
                image.raycastTarget = false;
                return root.AddComponent<Button>();
            }

            public sealed class CatalogueFixture
            {
                public CatalogueFixture(
                    Transform parent,
                    IReadOnlyList<FurnitureDefinitionAsset> definitions,
                    ICollection<UnityEngine.Object> ownedAssets)
                {
                    Root = UiObject("Task7Catalogue", parent);
                    var group = Root.AddComponent<CanvasGroup>();
                    View = Root.AddComponent<DecorationCatalogueView>();
                    Expanded = UiObject("Expanded", Root.transform);
                    Collapsed = UiObject("Collapsed", Root.transform);
                    var collapse = UiButton("CollapseButton", Expanded.transform);
                    var expand = UiButton("CollapsedHandleButton", Collapsed.transform);
                    Content = UiObject("Content", Expanded.transform);
                    ((RectTransform)Content.transform).sizeDelta = new Vector2(600f, 600f);
                    var templateObject = UiObject("TileTemplate", Content.transform);
                    var template = templateObject.AddComponent<DecorationCatalogueTileView>();
                    var button = UiButton("Button", templateObject.transform);
                    var thumbnail = UiObject("Thumbnail", templateObject.transform).AddComponent<Image>();
                    thumbnail.raycastTarget = false;
                    var name = UiObject("Name", templateObject.transform).AddComponent<TextMeshProUGUI>();
                    var footprint = UiObject("Footprint", templateObject.transform).AddComponent<TextMeshProUGUI>();
                    var warning = UiObject("Warning", templateObject.transform).AddComponent<TextMeshProUGUI>();
                    var warningShape = UiObject("WarningShape", templateObject.transform);
                    warningShape.AddComponent<Image>().raycastTarget = false;
                    SetField(template, "button", button);
                    SetField(template, "thumbnailImage", thumbnail);
                    SetField(template, "nameLabel", name);
                    SetField(template, "footprintLabel", footprint);
                    SetField(template, "warningLabel", warning);
                    SetField(template, "warningShape", warningShape);
                    templateObject.SetActive(false);
                    SetField(View, "canvasGroup", group);
                    SetField(View, "expandedRoot", Expanded);
                    SetField(View, "collapsedRoot", Collapsed);
                    SetField(View, "collapseButton", collapse);
                    SetField(View, "collapsedHandleButton", expand);
                    SetField(View, "contentRoot", Content.transform);
                    SetField(View, "tileTemplate", template);
                }

                public GameObject Root { get; }
                public DecorationCatalogueView View { get; }
                public GameObject Expanded { get; }
                public GameObject Collapsed { get; }
                public GameObject Content { get; }
            }

            public sealed class ActionFixture
            {
                private GameObject raycastRegion;

                public ActionFixture(Transform parent)
                {
                    Root = UiObject("Task7ActionBar", parent);
                    var group = Root.AddComponent<CanvasGroup>();
                    PointerHook = Root.AddComponent<DecorationPointerBoundaryEventHook>();
                    View = Root.AddComponent<DecorationActionBarView>();
                    Store = UiButton("StoreButton", Root.transform);
                    Rotate = UiButton("RotateButton", Root.transform);
                    Cancel = UiButton("CancelButton", Root.transform);
                    Confirm = UiButton("ConfirmButton", Root.transform);
                    Feedback = UiObject("Feedback", Root.transform).AddComponent<TextMeshProUGUI>();
                    StateShape = UiObject("StateShape", Root.transform);
                    StateShape.AddComponent<Image>().raycastTarget = false;
                    SetField(View, "canvasGroup", group);
                    SetField(View, "storeButton", Store);
                    SetField(View, "rotateButton", Rotate);
                    SetField(View, "cancelButton", Cancel);
                    SetField(View, "confirmButton", Confirm);
                    SetField(View, "feedbackLabel", Feedback);
                    SetField(View, "feedbackStateShape", StateShape);
                }

                public GameObject Root { get; }
                public DecorationPointerBoundaryEventHook PointerHook { get; }
                public DecorationActionBarView View { get; }
                public Button Store { get; }
                public Button Rotate { get; }
                public Button Cancel { get; }
                public Button Confirm { get; }
                public TMP_Text Feedback { get; }
                public GameObject StateShape { get; }

                public void ShowRaycastRegion(Vector2 screenPosition)
                {
                    if (raycastRegion == null)
                    {
                        raycastRegion = UiObject("ActionBarRaycastRegion", Root.transform);
                        raycastRegion.AddComponent<Image>().raycastTarget = true;
                        ((RectTransform)raycastRegion.transform).sizeDelta =
                            new Vector2(120f, 120f);
                    }

                    ((RectTransform)raycastRegion.transform).position = screenPosition;
                    raycastRegion.SetActive(true);
                    Canvas.ForceUpdateCanvases();
                }

                public void HideRaycastRegion()
                {
                    raycastRegion?.SetActive(false);
                    Canvas.ForceUpdateCanvases();
                }
            }

            public sealed class ModalFixture
            {
                public ModalFixture(Transform parent)
                {
                    Root = UiObject("Task7StoreModal", parent);
                    var group = Root.AddComponent<CanvasGroup>();
                    SharedModal = Root.AddComponent<AnimalCafeModalView>();
                    View = Root.AddComponent<DecorationStoreModalView>();
                    Blocker = UiButton("ModalBlocker", Root.transform);
                    Cancel = UiButton("CancelButton", Root.transform);
                    Confirm = UiButton("StoreButton", Root.transform);
                    Title = UiObject("Title", Root.transform).AddComponent<TextMeshProUGUI>();
                    Body = UiObject("Body", Root.transform).AddComponent<TextMeshProUGUI>();
                    SetField(View, "modalView", SharedModal);
                    SetField(View, "confirmButton", Confirm);
                    SetField(View, "cancelButton", Cancel);
                    SetField(View, "modalBlocker", Blocker);
                    SetField(View, "canvasGroup", group);
                    SetField(View, "titleLabel", Title);
                    SetField(View, "bodyLabel", Body);
                }

                public GameObject Root { get; }
                public AnimalCafeModalView SharedModal { get; }
                public DecorationStoreModalView View { get; }
                public Button Blocker { get; }
                public Button Cancel { get; }
                public Button Confirm { get; }
                public TMP_Text Title { get; }
                public TMP_Text Body { get; }
            }
        }

        private sealed class Task7QueuedCameraInput : MonoBehaviour, AnimalCafe.Input.ICameraInputSource
        {
            public AnimalCafe.Input.CameraInputFrame NextFrame { get; set; }

            public AnimalCafe.Input.CameraInputFrame ReadFrame()
            {
                var frame = NextFrame;
                NextFrame = default;
                return frame;
            }
        }

        private sealed class Task7FakeTouchSource : IDecorationTouchSource
        {
            private DecorationTouchPoint[] touches = Array.Empty<DecorationTouchPoint>();
            private int frameNumber;

            public void Queue(int frame, DecorationTouchPoint[] points)
            {
                frameNumber = frame;
                touches = points ?? Array.Empty<DecorationTouchPoint>();
            }

            public DecorationTouchFrame ReadFrame()
            {
                return new DecorationTouchFrame(frameNumber, touches);
            }
        }

        private sealed class Task7FakeMouseSource : IMouseDecorationInputSource
        {
            private DecorationTouchPoint[] touches = Array.Empty<DecorationTouchPoint>();
            private int frameNumber;
            private float scrollDelta;

            public bool HasActivePointer { get; private set; }
            public int ResetRequests { get; private set; }

            public void Queue(int frame, DecorationTouchPoint point, bool active)
            {
                frameNumber = frame;
                touches = new[] { point };
                HasActivePointer = active;
            }

            public void QueueScroll(float value)
            {
                scrollDelta = value;
            }

            public float ReadScrollDelta()
            {
                var value = scrollDelta;
                scrollDelta = 0f;
                return value;
            }

            public DecorationTouchFrame ReadFrame()
            {
                var frame = new DecorationTouchFrame(frameNumber, touches);
                touches = Array.Empty<DecorationTouchPoint>();
                return frame;
            }

            public void Reset()
            {
                ResetRequests++;
                touches = Array.Empty<DecorationTouchPoint>();
                scrollDelta = 0f;
                HasActivePointer = false;
            }
        }

        private sealed class Task7FakeGameTimeService : IGameTimeService
        {
            public Task7FakeGameTimeService(GameSpeed initialSpeed = GameSpeed.Normal)
            {
                CurrentSpeed = initialSpeed;
            }

            public GameSpeed CurrentSpeed { get; private set; }
            public int SetRequests { get; private set; }
            public bool RejectNextRequest { get; set; }
            public int RejectRequestsRemaining { get; set; }

            public bool TrySetSpeed(GameSpeed speed)
            {
                SetRequests++;
                if (RejectNextRequest)
                {
                    RejectNextRequest = false;
                    return false;
                }

                if (RejectRequestsRemaining > 0)
                {
                    RejectRequestsRemaining--;
                    return false;
                }

                CurrentSpeed = speed;
                return true;
            }
        }

        private readonly struct VisualCase
        {
            public VisualCase(
                string label,
                int width,
                int depth,
                GridPosition validAnchor,
                GridPosition invalidAnchor)
            {
                Label = label;
                Width = width;
                Depth = depth;
                ValidAnchor = validAnchor;
                InvalidAnchor = invalidAnchor;
            }

            public string Label { get; }
            public int Width { get; }
            public int Depth { get; }
            public GridPosition ValidAnchor { get; }
            public GridPosition InvalidAnchor { get; }
        }
    }

    public sealed class Task4Selectable : MonoBehaviour, ISelectable
    {
        public bool IsSelected { get; private set; }

        public void Select()
        {
            IsSelected = true;
        }

        public void Deselect()
        {
            IsSelected = false;
        }
    }
}

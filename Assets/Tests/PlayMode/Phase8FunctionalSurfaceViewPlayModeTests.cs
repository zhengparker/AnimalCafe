using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AnimalCafe.Content;
using AnimalCafe.Decoration;
using AnimalCafe.Layout;
using AnimalCafe.UI.Foundation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AnimalCafe.Tests.PlayMode
{
    public sealed class Phase8FunctionalSurfaceViewPlayModeTests
    {
        private const string SupportId = "00000000000000000000000000000001";
        private const string OtherSupportId = "00000000000000000000000000000002";
        private const string MountedIdA = "000000000000000000000000000000a1";
        private const string MountedIdB = "000000000000000000000000000000a2";
        private const string PickUpIdA = "000000000000000000000000000000b1";
        private const string PickUpIdB = "000000000000000000000000000000b2";
        private const string SupportDefinitionId = "furniture.counter.module.01";
        private const string MountedDefinitionId = "equipment.cash-register.task6";
        private const string FunctionalPreviewPrefabPath =
            "Assets/UI/Phase8/Prefabs/PF_UI_FunctionalSurfacePreview.prefab";
        private const string PickUpIndicatorPrefabPath =
            "Assets/UI/Phase8/Prefabs/PF_UI_PickUpPointIndicator.prefab";
        private const float Epsilon = 0.0001f;

        private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        private Material material;
        private AnimalCafeUiTheme theme;

        [SetUp]
        public void SetUp()
        {
            material = CreateMaterial();
            theme = ScriptableObject.CreateInstance<AnimalCafeUiTheme>();
            theme.Colors = new UiSemanticColorTokens
            {
                Accent = new Color(0.18f, 0.82f, 0.38f, 0.95f),
                Destructive = new Color(0.92f, 0.20f, 0.22f, 0.95f)
            };
            owned.Add(theme);
        }

        [TearDown]
        public void TearDown()
        {
            for (var index = owned.Count - 1; index >= 0; index--)
            {
                if (owned[index] != null)
                {
                    UnityEngine.Object.DestroyImmediate(owned[index]);
                }
            }

            owned.Clear();
        }

        [Test]
        public void CafeLayoutRuntime_InitializePublishesFunctionalLayoutAndFreshConfirmedReadiness()
        {
            var fixture = CreateRuntimeFixture(Vector3.up * 0.72f);

            fixture.Runtime.Initialize();

            Assert.That(fixture.Runtime.Layout, Is.Not.Null);
            Assert.That(fixture.Runtime.FunctionalSurfaceLayout, Is.Not.Null);
            Assert.That(fixture.Runtime.CurrentReadiness, Is.Not.Null);
            Assert.That(fixture.Runtime.CurrentReadiness.PickUpPoints.TotalCount, Is.Zero);
            var initialLayout = fixture.Runtime.Layout;
            var initialFunctionalLayout = fixture.Runtime.FunctionalSurfaceLayout;
            var initialReadiness = fixture.Runtime.CurrentReadiness;

            Assert.That(fixture.Runtime.FunctionalSurfaceLayout.PlacePickUp(
                new PickUpPointInstance(
                    PickUpIdA,
                    new SurfaceSlotAddress(SupportId, "slot.0"))).Succeeded,
                Is.True);
            Assert.That(fixture.Runtime.CurrentReadiness, Is.SameAs(initialReadiness),
                "Confirmed mutations do not silently replace the published snapshot.");

            fixture.Runtime.RecalculateReadiness();

            Assert.That(fixture.Runtime.CurrentReadiness, Is.Not.SameAs(initialReadiness));
            Assert.That(fixture.Runtime.CurrentReadiness.PickUpPoints.TotalCount, Is.EqualTo(1));
            fixture.Runtime.Initialize();
            Assert.That(fixture.Runtime.Layout, Is.SameAs(initialLayout));
            Assert.That(fixture.Runtime.FunctionalSurfaceLayout, Is.SameAs(initialFunctionalLayout));
            Assert.That(fixture.Runtime.CurrentReadiness.PickUpPoints.TotalCount, Is.EqualTo(1));
        }

        [Test]
        public void CafeLayoutRuntime_FailedPhase8SnapshotBuildPublishesNoPartialStateAndCanRetry()
        {
            var fixture = CreateRuntimeFixture(new Vector3(0.25f, 0.72f, 0f));

            Assert.Throws<ArgumentException>(() => fixture.Runtime.Initialize());
            Assert.That(fixture.Runtime.Layout, Is.Null);
            Assert.That(fixture.Runtime.FunctionalSurfaceLayout, Is.Null);
            Assert.That(fixture.Runtime.CurrentReadiness, Is.Null);

            fixture.Marker.transform.localPosition = Vector3.up * 0.72f;
            Assert.DoesNotThrow(() => fixture.Runtime.Initialize());
            Assert.That(fixture.Runtime.Layout, Is.Not.Null);
            Assert.That(fixture.Runtime.FunctionalSurfaceLayout, Is.Not.Null);
            Assert.That(fixture.Runtime.CurrentReadiness, Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator SurfaceMountedSceneRegistry_RebuildsMultipleConfirmedIdsAndFollowsSupportMoveRotate()
        {
            var fixture = CreateFunctionalFixture();
            var root = CreateObject("SurfaceMountedRepresentations");
            var sentinel = CreateObject("SurfaceMounted_000000000000000000000000000000a1");
            sentinel.transform.SetParent(root.transform, false);
            var registry = CreateObject("SurfaceMountedRegistry")
                .AddComponent<SurfaceMountedSceneRegistry>();
            registry.Configure(fixture.Catalog, fixture.FurnitureRegistry, root.transform);
            var instances = new[]
            {
                new SurfaceMountedInstance(
                    MountedIdA,
                    MountedDefinitionId,
                    new SurfaceSlotAddress(SupportId, "slot.0"),
                    FurnitureRotation.Degrees0),
                new SurfaceMountedInstance(
                    MountedIdB,
                    MountedDefinitionId,
                    new SurfaceSlotAddress(SupportId, "slot.1"),
                    FurnitureRotation.Degrees90)
            };

            registry.Rebuild(instances);

            Assert.That(registry.TryGet(MountedIdA, out var first), Is.True);
            Assert.That(registry.TryGet(MountedIdB, out var second), Is.True);
            Assert.That(first, Is.Not.SameAs(sentinel),
                "A misleading Scene name must not become business identity.");
            Assert.That(ActiveRenderers(first), Is.Not.Empty);
            Assert.That(ActiveRenderers(second), Is.Not.Empty);
            var firstPosition = first.transform.position;
            AssertVector(firstPosition, SlotWorldPosition(fixture, "slot.0"));
            AssertVector(second.transform.position, SlotWorldPosition(fixture, "slot.1"));

            Assert.That(fixture.Layout.MoveFurniture(SupportId, new GridPosition(4, 3)).Succeeded,
                Is.True);
            Assert.That(fixture.Layout.RotateFurniture(
                SupportId,
                FurnitureRotation.Degrees90).Succeeded,
                Is.True);
            fixture.FurnitureRegistry.Rebuild(fixture.Layout.FurnitureInstances);
            registry.Rebuild(instances);

            Assert.That(registry.TryGet(MountedIdA, out var movedFirst), Is.True);
            Assert.That(registry.TryGet(MountedIdB, out var movedSecond), Is.True);
            AssertVector(movedFirst.transform.position, SlotWorldPosition(fixture, "slot.0"));
            AssertVector(movedSecond.transform.position, SlotWorldPosition(fixture, "slot.1"));
            Assert.That(Vector3.Distance(firstPosition, movedFirst.transform.position),
                Is.GreaterThan(1f));
            Assert.That(Quaternion.Angle(
                movedSecond.transform.rotation,
                fixture.SupportRepresentation.transform.rotation * Quaternion.Euler(0f, 90f, 0f)),
                Is.LessThan(Epsilon));

            registry.Rebuild(Array.Empty<SurfaceMountedInstance>());
            Assert.That(movedFirst.activeSelf, Is.False);
            Assert.That(movedSecond.activeSelf, Is.False);
            Assert.That(sentinel, Is.Not.Null);
            Assert.That(sentinel.transform.parent, Is.SameAs(root.transform));
            yield return null;
            Assert.That(registry.TryGet(MountedIdA, out _), Is.False);
            Assert.That(registry.TryGet(MountedIdB, out _), Is.False);
        }

        [Test]
        public void SurfaceMountedSceneRegistry_CorruptBindingDoesNotGuessOrDestroyUnownedChildren()
        {
            var fixture = CreateFunctionalFixture();
            var root = CreateObject("CorruptMountedRoot");
            var sentinel = CreateObject("UnownedSentinel");
            sentinel.transform.SetParent(root.transform, false);
            var registry = CreateObject("CorruptMountedRegistry")
                .AddComponent<SurfaceMountedSceneRegistry>();
            registry.Configure(fixture.Catalog, fixture.FurnitureRegistry, root.transform);
            var corrupt = new SurfaceMountedInstance(
                MountedIdA,
                MountedDefinitionId,
                new SurfaceSlotAddress(
                    "ffffffffffffffffffffffffffffffff",
                    "slot.0"),
                FurnitureRotation.Degrees0);

            Assert.DoesNotThrow(() => registry.Rebuild(new[] { corrupt }));
            Assert.That(registry.TryGet(MountedIdA, out _), Is.False);
            Assert.That(sentinel, Is.Not.Null);
            Assert.That(sentinel.transform.parent, Is.SameAs(root.transform));
        }

        [Test]
        public void ConfirmedFunctionalViews_ProjectOnlyBoundContentAcrossEverySupportQuarterTurn()
        {
            var fixture = CreateFunctionalFixture();
            Assert.That(fixture.Layout.PlaceFurniture(FurnitureInstance.Restore(
                OtherSupportId,
                SupportDefinitionId,
                new GridPosition(6, 0),
                FurnitureRotation.Degrees0)).Succeeded, Is.True);
            fixture.FurnitureRegistry.Rebuild(fixture.Layout.FurnitureInstances);
            var mounted = new[]
            {
                new SurfaceMountedInstance(
                    MountedIdA,
                    MountedDefinitionId,
                    new SurfaceSlotAddress(SupportId, "slot.0"),
                    FurnitureRotation.Degrees90),
                new SurfaceMountedInstance(
                    MountedIdB,
                    MountedDefinitionId,
                    new SurfaceSlotAddress(OtherSupportId, "slot.0"),
                    FurnitureRotation.Degrees270)
            };
            var points = new[]
            {
                new PickUpPointInstance(
                    PickUpIdA,
                    new SurfaceSlotAddress(SupportId, "slot.2")),
                new PickUpPointInstance(
                    PickUpIdB,
                    new SurfaceSlotAddress(SupportId, "slot.3"))
            };
            var mountedRoot = CreateObject("ProjectedMountedRoot");
            var mountedRegistry = CreateObject("ProjectedMountedRegistry")
                .AddComponent<SurfaceMountedSceneRegistry>();
            mountedRegistry.Configure(
                fixture.Catalog,
                fixture.FurnitureRegistry,
                mountedRoot.transform);
            mountedRegistry.Rebuild(mounted);
            var pickUpRoot = CreateObject("ProjectedPickUpRoot");
            var pickUpView = CreateObject("ProjectedPickUpView")
                .AddComponent<PickUpPointIndicatorView>();
            pickUpView.Configure(
                fixture.FurnitureRegistry,
                pickUpRoot.transform,
                CreatePickUpIndicatorTemplate(),
                theme);
            pickUpView.Rebuild(points, true);

            Assert.That(mountedRegistry.TryGet(MountedIdA, out var projectedMounted), Is.True);
            Assert.That(mountedRegistry.TryGet(MountedIdB, out var otherMounted), Is.True);
            Assert.That(pickUpView.TryGet(PickUpIdA, out var firstPickUp), Is.True);
            Assert.That(pickUpView.TryGet(PickUpIdB, out var secondPickUp), Is.True);
            var otherPosition = otherMounted.transform.position;
            var previewSupport = UnityEngine.Object.Instantiate(fixture.SupportRepresentation);
            owned.Add(previewSupport);
            previewSupport.name = "SupportPreviewClone";
            previewSupport.SetActive(true);

            var cases = new[]
            {
                (FurnitureRotation.Degrees0,
                    new Vector3(10f, 0.72f, 18.5f),
                    new Vector3(10f, 0.72f, 20.5f),
                    new Vector3(10f, 0.72f, 21.5f)),
                (FurnitureRotation.Degrees90,
                    new Vector3(8.5f, 0.72f, 20f),
                    new Vector3(10.5f, 0.72f, 20f),
                    new Vector3(11.5f, 0.72f, 20f)),
                (FurnitureRotation.Degrees180,
                    new Vector3(10f, 0.72f, 21.5f),
                    new Vector3(10f, 0.72f, 19.5f),
                    new Vector3(10f, 0.72f, 18.5f)),
                (FurnitureRotation.Degrees270,
                    new Vector3(11.5f, 0.72f, 20f),
                    new Vector3(9.5f, 0.72f, 20f),
                    new Vector3(8.5f, 0.72f, 20f))
            };

            foreach (var item in cases)
            {
                previewSupport.transform.SetPositionAndRotation(
                    new Vector3(10f, 0f, 20f),
                    Quaternion.Euler(0f, (int)item.Item1, 0f));

                mountedRegistry.ProjectSupportPreview(SupportId, previewSupport.transform);
                pickUpView.ProjectSupportPreview(SupportId, previewSupport.transform);

                Assert.That(mountedRegistry.TryGet(MountedIdA, out var sameMounted), Is.True);
                Assert.That(sameMounted, Is.SameAs(projectedMounted));
                Assert.That(pickUpView.TryGet(PickUpIdA, out var sameFirstPickUp), Is.True);
                Assert.That(sameFirstPickUp, Is.SameAs(firstPickUp));
                Assert.That(pickUpView.TryGet(PickUpIdB, out var sameSecondPickUp), Is.True);
                Assert.That(sameSecondPickUp, Is.SameAs(secondPickUp));
                AssertVector(projectedMounted.transform.position, item.Item2);
                AssertVector(firstPickUp.transform.position, item.Item3);
                AssertVector(secondPickUp.transform.position, item.Item4);
                Assert.That(Quaternion.Angle(
                    projectedMounted.transform.rotation,
                    Quaternion.Euler(0f, (int)item.Item1 + 90f, 0f)),
                    Is.LessThan(Epsilon));
                AssertVector(otherMounted.transform.position, otherPosition);
                Assert.That(otherMounted.activeSelf, Is.True);
            }

            Assert.That(mounted, Has.Length.EqualTo(2));
            Assert.That(points, Has.Length.EqualTo(2));
            Assert.That(projectedMounted.transform.parent, Is.SameAs(mountedRoot.transform));
            Assert.That(firstPickUp.transform.parent, Is.SameAs(pickUpRoot.transform));
        }

        [Test]
        public void ConfirmedFunctionalViews_PreviewSlotMissingOrDuplicate_HidesOnlyAffectedBinding()
        {
            var fixture = CreateFunctionalFixture();
            var mounted = new[]
            {
                new SurfaceMountedInstance(
                    MountedIdA,
                    MountedDefinitionId,
                    new SurfaceSlotAddress(SupportId, "slot.0"),
                    FurnitureRotation.Degrees0),
                new SurfaceMountedInstance(
                    MountedIdB,
                    MountedDefinitionId,
                    new SurfaceSlotAddress(SupportId, "slot.1"),
                    FurnitureRotation.Degrees0)
            };
            var points = new[]
            {
                new PickUpPointInstance(
                    PickUpIdA,
                    new SurfaceSlotAddress(SupportId, "slot.2")),
                new PickUpPointInstance(
                    PickUpIdB,
                    new SurfaceSlotAddress(SupportId, "slot.3"))
            };
            var mountedRegistry = CreateObject("FailClosedMountedRegistry")
                .AddComponent<SurfaceMountedSceneRegistry>();
            mountedRegistry.Configure(
                fixture.Catalog,
                fixture.FurnitureRegistry,
                CreateObject("FailClosedMountedRoot").transform);
            mountedRegistry.Rebuild(mounted);
            var pickUpView = CreateObject("FailClosedPickUpView")
                .AddComponent<PickUpPointIndicatorView>();
            pickUpView.Configure(
                fixture.FurnitureRegistry,
                CreateObject("FailClosedPickUpRoot").transform,
                CreatePickUpIndicatorTemplate(),
                theme);
            pickUpView.Rebuild(points, true);
            Assert.That(mountedRegistry.TryGet(MountedIdA, out var duplicateAffected), Is.True);
            Assert.That(mountedRegistry.TryGet(MountedIdB, out var mountedUnaffected), Is.True);
            Assert.That(pickUpView.TryGet(PickUpIdA, out var missingAffected), Is.True);
            Assert.That(pickUpView.TryGet(PickUpIdB, out var pickUpUnaffected), Is.True);
            var previewSupport = UnityEngine.Object.Instantiate(fixture.SupportRepresentation);
            owned.Add(previewSupport);
            previewSupport.name = "CorruptSupportPreviewClone";
            previewSupport.SetActive(true);
            previewSupport.transform.SetPositionAndRotation(
                new Vector3(10f, 0f, 20f),
                Quaternion.identity);
            var slotTwo = previewSupport.GetComponentsInChildren<SurfaceSlotMarker>(true)
                .Single(marker => marker.SlotId == "slot.2");
            UnityEngine.Object.DestroyImmediate(slotTwo.gameObject);
            var duplicateObject = CreateObject("DuplicateSlotZero");
            duplicateObject.transform.SetParent(previewSupport.transform, false);
            duplicateObject.transform.localPosition = new Vector3(4f, 0.72f, 0f);
            var duplicateMarker = duplicateObject.AddComponent<SurfaceSlotMarker>();
            SetField(duplicateMarker, "slotId", "slot.0");

            mountedRegistry.ProjectSupportPreview(SupportId, previewSupport.transform);
            pickUpView.ProjectSupportPreview(SupportId, previewSupport.transform);

            Assert.That(duplicateAffected.activeSelf, Is.False,
                "Duplicate preview Slot markers must fail closed instead of leaving old world state.");
            Assert.That(missingAffected.activeSelf, Is.False,
                "A missing preview Slot marker must fail closed instead of leaving old world state.");
            Assert.That(mountedUnaffected.activeSelf, Is.True);
            AssertVector(mountedUnaffected.transform.position, new Vector3(10f, 0.72f, 19.5f));
            Assert.That(pickUpUnaffected.activeSelf, Is.True);
            AssertVector(pickUpUnaffected.transform.position, new Vector3(10f, 0.72f, 21.5f));
        }

        [TestCase(MountedDefinitionId, FurnitureFunctionType.CashRegister)]
        [TestCase("equipment.coffee-machine.task6", FurnitureFunctionType.CoffeeMachine)]
        public void SurfaceMountedPreviewView_ValidityChangesOnlyFootprintAndPreservesAuthoredModel(
            string definitionId,
            FurnitureFunctionType functionType)
        {
            var originalColor = new Color(0.61f, 0.42f, 0.26f, 1f);
            material.color = originalColor;
            var originalTexture = new Texture2D(2, 2);
            owned.Add(originalTexture);
            material.mainTexture = originalTexture;
            var fixture = CreateFunctionalFixture(definitionId, functionType);
            Assert.That(fixture.Catalog.TryGetDefinitionAsset(definitionId, out var definition), Is.True);
            var previewTemplate = CreateFunctionalPreviewTemplate();
            var previewRoot = CreateObject("FunctionalPreviewRoot");
            var view = CreateObject("FunctionalPreviewView")
                .AddComponent<SurfaceMountedPreviewView>();
            view.Configure(
                fixture.Catalog,
                fixture.FurnitureRegistry,
                previewRoot.transform,
                previewTemplate,
                theme);
            var session = new FunctionalSurfaceDecorationSession(fixture.FunctionalLayout);

            Assert.That(session.BeginCreateMounted(
                definitionId,
                new SurfaceSlotAddress(SupportId, "slot.0")).Succeeded,
                Is.True);
            view.Show(session.ActivePreview);

            Assert.That(fixture.FunctionalLayout.MountedInstances, Is.Empty,
                "A Scene preview must not publish confirmed domain state.");
            Assert.That(view.CurrentGhost, Is.Not.Null);
            Assert.That(view.CurrentFootprint, Is.Not.Null);
            AssertVector(view.CurrentGhost.transform.position, SlotWorldPosition(fixture, "slot.0"));
            AssertColor(ActiveRenderers(view.CurrentFootprint).First(), theme.Colors.Accent);
            AssertAuthoredModelAppearance(view.CurrentGhost, originalColor, originalTexture);
            AssertAuthoredModelAppearance(definition.Prefab, originalColor, originalTexture);

            Assert.That(fixture.FunctionalLayout.PlacePickUp(new PickUpPointInstance(
                PickUpIdA,
                new SurfaceSlotAddress(SupportId, "slot.1"))).Succeeded,
                Is.True);
            Assert.That(session.MovePreview(
                new SurfaceSlotAddress(SupportId, "slot.1")).Succeeded,
                Is.False);
            view.Show(session.ActivePreview);

            Assert.That(view.CurrentGhost, Is.Not.Null);
            AssertVector(view.CurrentGhost.transform.position, SlotWorldPosition(fixture, "slot.1"));
            AssertColor(ActiveRenderers(view.CurrentFootprint).First(), theme.Colors.Destructive);
            AssertAuthoredModelAppearance(view.CurrentGhost, originalColor, originalTexture);
            AssertAuthoredModelAppearance(definition.Prefab, originalColor, originalTexture);
            Assert.That(fixture.FunctionalLayout.MountedInstances, Is.Empty);
            Assert.That(fixture.FunctionalLayout.PickUpPoints, Has.Count.EqualTo(1));

            Assert.That(session.MovePreview(
                new SurfaceSlotAddress(SupportId, "slot.0")).Succeeded,
                Is.True);
            view.Show(session.ActivePreview);
            AssertVector(view.CurrentGhost.transform.position, SlotWorldPosition(fixture, "slot.0"));
            AssertColor(ActiveRenderers(view.CurrentFootprint).First(), theme.Colors.Accent);
            AssertAuthoredModelAppearance(view.CurrentGhost, originalColor, originalTexture);
            AssertAuthoredModelAppearance(definition.Prefab, originalColor, originalTexture);
            Assert.That(fixture.FunctionalLayout.MountedInstances, Is.Empty);
            Assert.That(fixture.FunctionalLayout.PickUpPoints, Has.Count.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator SurfaceMountedPreviewView_RepeatedShowImmediatelyDeactivatesOldOwnedChildrenAndDestroysThemNextFrame()
        {
            var fixture = CreateFunctionalFixture();
            var previewRoot = CreateObject("RepeatedMountedPreviewRoot");
            var view = CreateObject("RepeatedMountedPreviewView")
                .AddComponent<SurfaceMountedPreviewView>();
            view.Configure(
                fixture.Catalog,
                fixture.FurnitureRegistry,
                previewRoot.transform,
                CreateFunctionalPreviewTemplate(),
                theme);
            var session = new FunctionalSurfaceDecorationSession(fixture.FunctionalLayout);
            Assert.That(session.BeginCreateMounted(
                MountedDefinitionId,
                new SurfaceSlotAddress(SupportId, "slot.0")).Succeeded,
                Is.True);

            view.Show(session.ActivePreview);
            var firstGhost = view.CurrentGhost;
            var firstFootprint = view.CurrentFootprint;
            AssertDirectActiveChildren(
                previewRoot.transform,
                firstGhost,
                firstFootprint);

            Assert.That(session.MovePreview(
                new SurfaceSlotAddress(SupportId, "slot.1")).Succeeded,
                Is.True);
            view.Show(session.ActivePreview);

            Assert.That(view.CurrentGhost, Is.Not.Null);
            Assert.That(view.CurrentFootprint, Is.Not.Null);
            Assert.That(view.CurrentGhost, Is.Not.SameAs(firstGhost));
            Assert.That(view.CurrentFootprint, Is.Not.SameAs(firstFootprint));
            AssertDirectActiveChildren(
                previewRoot.transform,
                view.CurrentGhost,
                view.CurrentFootprint);

            yield return null;

            Assert.That(previewRoot.transform.childCount, Is.EqualTo(2));
            AssertDirectChildren(
                previewRoot.transform,
                view.CurrentGhost,
                view.CurrentFootprint);
        }

        [UnityTest]
        public IEnumerator PickUpPointIndicatorView_HidesAllOutsideDecorationAndRebuildsMultipleAtCurrentSupportTransform()
        {
            var fixture = CreateFunctionalFixture();
            Assert.That(fixture.FunctionalLayout.PlacePickUp(new PickUpPointInstance(
                PickUpIdA,
                new SurfaceSlotAddress(SupportId, "slot.0"))).Succeeded,
                Is.True);
            Assert.That(fixture.FunctionalLayout.PlacePickUp(new PickUpPointInstance(
                PickUpIdB,
                new SurfaceSlotAddress(SupportId, "slot.1"))).Succeeded,
                Is.True);
            var template = CreatePickUpIndicatorTemplate();
            var root = CreateObject("PickUpIndicators");
            var sentinel = CreateObject("UnownedIndicatorSentinel");
            sentinel.transform.SetParent(root.transform, false);
            var view = CreateObject("PickUpIndicatorView")
                .AddComponent<PickUpPointIndicatorView>();
            view.Configure(fixture.FurnitureRegistry, root.transform, template, theme);

            view.Rebuild(fixture.FunctionalLayout.PickUpPoints, true);
            Assert.That(view.TryGet(PickUpIdA, out var first), Is.True);
            Assert.That(view.TryGet(PickUpIdB, out var second), Is.True);
            AssertVector(first.transform.position, SlotWorldPosition(fixture, "slot.0"));
            AssertVector(second.transform.position, SlotWorldPosition(fixture, "slot.1"));
            AssertPickUpStateColor(first, theme.Colors.Accent);
            AssertPickUpStateColor(second, theme.Colors.Accent);

            view.Rebuild(fixture.FunctionalLayout.PickUpPoints, false);
            Assert.That(first.activeSelf, Is.False);
            Assert.That(second.activeSelf, Is.False);
            Assert.That(view.TryGet(PickUpIdA, out _), Is.False);
            Assert.That(view.TryGet(PickUpIdB, out _), Is.False);

            Assert.That(fixture.Layout.MoveFurniture(SupportId, new GridPosition(4, 3)).Succeeded,
                Is.True);
            Assert.That(fixture.Layout.RotateFurniture(
                SupportId,
                FurnitureRotation.Degrees90).Succeeded,
                Is.True);
            fixture.FurnitureRegistry.Rebuild(fixture.Layout.FurnitureInstances);
            view.Rebuild(fixture.FunctionalLayout.PickUpPoints, true);

            Assert.That(view.TryGet(PickUpIdA, out var rebuiltFirst), Is.True);
            Assert.That(view.TryGet(PickUpIdB, out var rebuiltSecond), Is.True);
            AssertVector(rebuiltFirst.transform.position, SlotWorldPosition(fixture, "slot.0"));
            AssertVector(rebuiltSecond.transform.position, SlotWorldPosition(fixture, "slot.1"));
            Assert.That(sentinel, Is.Not.Null);
            Assert.That(sentinel.transform.parent, Is.SameAs(root.transform));
            yield return null;
        }

        [Test]
        public void PickUpPointIndicatorView_PreviewColorsOnlyFootprintAndPreservesNeutralPyramid()
        {
            var fixture = CreateFunctionalFixture();
            var template = CreatePickUpIndicatorTemplate();
            var view = CreateObject("PickUpPreviewIndicatorView")
                .AddComponent<PickUpPointIndicatorView>();
            view.Configure(
                fixture.FurnitureRegistry,
                CreateObject("PickUpPreviewRoot").transform,
                template,
                theme);
            var session = new FunctionalSurfaceDecorationSession(fixture.FunctionalLayout);

            view.Rebuild(fixture.FunctionalLayout.PickUpPoints, true);
            Assert.That(session.BeginCreatePickUp(
                new SurfaceSlotAddress(SupportId, "slot.0")).Succeeded,
                Is.True);
            view.ShowPreview(session.ActivePreview);

            AssertIndicatorGeometry(view.CurrentPreview);
            AssertPickUpStateColor(view.CurrentPreview, theme.Colors.Accent);
            Assert.That(fixture.FunctionalLayout.PickUpPoints, Is.Empty,
                "Showing the Preview must not Confirm it.");

            Assert.That(fixture.FunctionalLayout.PlacePickUp(new PickUpPointInstance(
                PickUpIdA,
                new SurfaceSlotAddress(SupportId, "slot.1"))).Succeeded,
                Is.True);
            Assert.That(session.MovePreview(
                new SurfaceSlotAddress(SupportId, "slot.1")).Succeeded,
                Is.False);
            view.ShowPreview(session.ActivePreview);
            AssertPickUpStateColor(view.CurrentPreview, theme.Colors.Destructive);

            view.Rebuild(fixture.FunctionalLayout.PickUpPoints, false);
            view.ShowPreview(session.ActivePreview);
            Assert.That(view.CurrentPreview, Is.Null,
                "Confirmed and Preview indicators must both stay hidden outside Decoration Mode.");
        }

        [Test]
        public void PickUpOverlap_InvalidPreviewHidesOnlyOccupiedSlotAndCancelRestoresIt()
        {
            var fixture = CreatePickUpOverlapFixture(out var view, out _);
            view.TryGet(PickUpIdA, out var first);
            view.TryGet(PickUpIdB, out var second);
            var session = new FunctionalSurfaceDecorationSession(fixture.FunctionalLayout);
            var address = new SurfaceSlotAddress(SupportId, "slot.0");
            Assert.That(session.BeginCreatePickUp(address).Succeeded, Is.False);
            view.ShowPreview(session.ActivePreview);

            Assert.That(VisiblePickUpRenderers(first), Is.Empty,
                "The occupied slot must show only the red preview, without a second sign or green footprint.");
            Assert.That(VisiblePickUpRenderers(second), Has.Length.EqualTo(2));
            Assert.That(VisiblePickUpRenderers(view.CurrentPreview), Has.Length.EqualTo(2));
            AssertPickUpStateColor(view.CurrentPreview, theme.Colors.Destructive);
            Assert.That(session.ActivePreview.CanConfirm, Is.False);
            Assert.That(session.Confirm().Succeeded, Is.False, "Visual hiding must not free the occupied slot.");
            Assert.That(fixture.FunctionalLayout.PickUpPoints.Select(point => point.InstanceId),
                Is.EquivalentTo(new[] { PickUpIdA, PickUpIdB }));
            Assert.That(fixture.FunctionalLayout.PickUpPoints.Single(point => point.InstanceId == PickUpIdA).Address,
                Is.EqualTo(address));

            session.Cancel();
            view.HidePreview();
            view.HidePreview();
            Assert.That(view.CurrentPreview, Is.Null);
            Assert.That(VisiblePickUpRenderers(first), Has.Length.EqualTo(2));
            Assert.That(VisiblePickUpRenderers(second), Has.Length.EqualTo(2));
            AssertPickUpStateColor(first, theme.Colors.Accent);
        }

        [Test]
        public void PickUpOverlap_MovingPreviewRestoresPreviousSlot()
        {
            var fixture = CreatePickUpOverlapFixture(out var view, out _);
            view.TryGet(PickUpIdA, out var first);
            view.TryGet(PickUpIdB, out var second);
            var session = new FunctionalSurfaceDecorationSession(fixture.FunctionalLayout);
            session.BeginCreatePickUp(new SurfaceSlotAddress(SupportId, "slot.0"));
            view.ShowPreview(session.ActivePreview);
            Assert.That(VisiblePickUpRenderers(first), Is.Empty);

            Assert.That(session.MovePreview(new SurfaceSlotAddress(SupportId, "slot.1")).Succeeded, Is.False);
            view.ShowPreview(session.ActivePreview);
            view.ShowPreview(session.ActivePreview);
            Assert.That(VisiblePickUpRenderers(first), Has.Length.EqualTo(2));
            Assert.That(VisiblePickUpRenderers(second), Is.Empty);
            Assert.That(session.MovePreview(new SurfaceSlotAddress(SupportId, "slot.2")).Succeeded, Is.True);
            view.ShowPreview(session.ActivePreview);
            Assert.That(VisiblePickUpRenderers(first), Has.Length.EqualTo(2));
            Assert.That(VisiblePickUpRenderers(second), Has.Length.EqualTo(2));
        }

        [Test]
        public void PickUpOverlap_HidePreviewDoesNotRestoreControllerHiddenSource()
        {
            var fixture = CreatePickUpOverlapFixture(out var view, out _);
            view.TryGet(PickUpIdA, out var source);
            view.TryGet(PickUpIdB, out var target);
            var session = new FunctionalSurfaceDecorationSession(fixture.FunctionalLayout);
            Assert.That(session.BeginMovePickUp(PickUpIdA).Succeeded, Is.True);
            // Controller hides its source before refreshing visuals. Only it may restore that source.
            // 保持真实调用顺序；View 不得恢复 controller 已隐藏的原件。
            source.SetActive(false);
            view.ShowPreview(session.ActivePreview);
            Assert.That(session.MovePreview(new SurfaceSlotAddress(SupportId, "slot.1")).Succeeded, Is.False);
            source.SetActive(false);
            view.HidePreview();
            view.ShowPreview(session.ActivePreview);
            Assert.That(VisiblePickUpRenderers(target), Is.Empty);
            view.ShowPreview(session.ActivePreview);
            view.HidePreview();
            Assert.That(source.activeSelf, Is.False, "Preview cleanup must not revive the controller's source.");
            Assert.That(VisiblePickUpRenderers(target), Has.Length.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator PickUpOverlap_RebuildAndModeExitDiscardOldVisibilityOwnership()
        {
            var fixture = CreatePickUpOverlapFixture(out var view, out var root);
            view.TryGet(PickUpIdA, out var oldConfirmed);
            var session = new FunctionalSurfaceDecorationSession(fixture.FunctionalLayout);
            session.BeginCreatePickUp(new SurfaceSlotAddress(SupportId, "slot.0"));
            view.ShowPreview(session.ActivePreview);
            Assert.That(VisiblePickUpRenderers(oldConfirmed), Is.Empty);
            view.Rebuild(fixture.FunctionalLayout.PickUpPoints, true);
            Assert.That(view.TryGet(PickUpIdA, out var rebuilt), Is.True);
            Assert.That(rebuilt, Is.Not.SameAs(oldConfirmed));
            view.HidePreview();
            Assert.That(oldConfirmed.activeSelf, Is.False, "Rebuild must not revive old representations.");
            Assert.That(VisiblePickUpRenderers(rebuilt), Has.Length.EqualTo(2));
            view.ShowPreview(session.ActivePreview);
            Assert.That(VisiblePickUpRenderers(rebuilt), Is.Empty);
            view.Rebuild(fixture.FunctionalLayout.PickUpPoints, false);
            view.HidePreview();
            view.ShowPreview(session.ActivePreview);
            Assert.That(view.CurrentPreview, Is.Null);
            Assert.That(view.TryGet(PickUpIdA, out _), Is.False);
            Assert.That(VisiblePickUpRenderers(root), Is.Empty);
            yield return null;
            Assert.That(root.transform.childCount, Is.Zero);
            view.Rebuild(fixture.FunctionalLayout.PickUpPoints, true);
            Assert.That(view.TryGet(PickUpIdA, out var reentered), Is.True);
            Assert.That(VisiblePickUpRenderers(reentered), Has.Length.EqualTo(2));
            Assert.That(fixture.FunctionalLayout.PickUpPoints.Count, Is.EqualTo(2));
        }

        private FunctionalFixture CreatePickUpOverlapFixture(out PickUpPointIndicatorView view, out GameObject root)
        {
            var fixture = CreateFunctionalFixture();
            Assert.That(fixture.FunctionalLayout.PlacePickUp(new PickUpPointInstance(
                PickUpIdA, new SurfaceSlotAddress(SupportId, "slot.0"))).Succeeded, Is.True);
            Assert.That(fixture.FunctionalLayout.PlacePickUp(new PickUpPointInstance(
                PickUpIdB, new SurfaceSlotAddress(SupportId, "slot.1"))).Succeeded, Is.True);
            root = CreateObject("PickUpOverlapRoot");
            view = CreateObject("PickUpOverlapView").AddComponent<PickUpPointIndicatorView>();
            view.Configure(fixture.FurnitureRegistry, root.transform, CreatePickUpIndicatorTemplate(), theme);
            view.Rebuild(fixture.FunctionalLayout.PickUpPoints, true);
            return fixture;
        }

        private static Renderer[] VisiblePickUpRenderers(GameObject target)
        {
            return target.GetComponentsInChildren<Renderer>(true)
                .Where(renderer => renderer.enabled && renderer.gameObject.activeInHierarchy).ToArray();
        }

        [UnityTest]
        public IEnumerator PickUpPointIndicatorView_RepeatedPreviewHideAndModeExitLeaveOnlyExpectedOwnedChildren()
        {
            var fixture = CreateFunctionalFixture();
            Assert.That(fixture.FunctionalLayout.PlacePickUp(new PickUpPointInstance(
                PickUpIdA,
                new SurfaceSlotAddress(SupportId, "slot.0"))).Succeeded,
                Is.True);
            var root = CreateObject("PickUpLifecycleRoot");
            var view = CreateObject("PickUpLifecycleView")
                .AddComponent<PickUpPointIndicatorView>();
            view.Configure(
                fixture.FurnitureRegistry,
                root.transform,
                CreatePickUpIndicatorTemplate(),
                theme);
            view.Rebuild(fixture.FunctionalLayout.PickUpPoints, true);
            Assert.That(view.TryGet(PickUpIdA, out var confirmed), Is.True);
            var session = new FunctionalSurfaceDecorationSession(fixture.FunctionalLayout);
            Assert.That(session.BeginCreatePickUp(
                new SurfaceSlotAddress(SupportId, "slot.1")).Succeeded,
                Is.True);

            view.ShowPreview(session.ActivePreview);
            var firstPreview = view.CurrentPreview;
            AssertDirectActiveChildren(root.transform, confirmed, view.CurrentPreview);

            view.ShowPreview(session.ActivePreview);

            Assert.That(view.CurrentPreview, Is.Not.Null);
            Assert.That(view.CurrentPreview, Is.Not.SameAs(firstPreview));
            AssertDirectActiveChildren(root.transform, confirmed, view.CurrentPreview);

            yield return null;

            Assert.That(root.transform.childCount, Is.EqualTo(2));
            AssertDirectChildren(root.transform, confirmed, view.CurrentPreview);

            view.HidePreview();
            Assert.That(view.CurrentPreview, Is.Null);
            AssertDirectActiveChildren(root.transform, confirmed);

            yield return null;

            Assert.That(root.transform.childCount, Is.EqualTo(1));
            AssertDirectChildren(root.transform, confirmed);

            view.ShowPreview(session.ActivePreview);
            Assert.That(view.CurrentPreview, Is.Not.Null);
            AssertDirectActiveChildren(root.transform, confirmed, view.CurrentPreview);

            view.Rebuild(fixture.FunctionalLayout.PickUpPoints, false);
            Assert.That(view.CurrentPreview, Is.Null);
            Assert.That(view.TryGet(PickUpIdA, out _), Is.False);
            AssertDirectActiveChildren(root.transform);

            yield return null;

            Assert.That(root.transform.childCount, Is.Zero);
            AssertDirectChildren(root.transform);
        }

#if UNITY_EDITOR
        [Test]
        public void ProductionFunctionalSurfacePreviewPrefab_IsPersistentRenderOnlyAndUsableByRuntimeView()
        {
            var prefab = LoadEditorAsset<GameObject>(FunctionalPreviewPrefabPath);

            Assert.That(prefab, Is.Not.Null, FunctionalPreviewPrefabPath);
            AssertRenderChild(prefab.transform, "Footprint");
            AssertRenderChild(prefab.transform, "FeedbackMarker");
            AssertNoPhysicsOrNavigationComponents(prefab);

            var fixture = CreateFunctionalFixture();
            var ownedRoot = CreateObject("ProductionFunctionalPreviewOwnedRoot");
            var view = CreateObject("ProductionFunctionalPreviewView")
                .AddComponent<SurfaceMountedPreviewView>();
            view.Configure(
                fixture.Catalog,
                fixture.FurnitureRegistry,
                ownedRoot.transform,
                prefab,
                theme);
            var session = new FunctionalSurfaceDecorationSession(fixture.FunctionalLayout);
            Assert.That(session.BeginCreateMounted(
                MountedDefinitionId,
                new SurfaceSlotAddress(SupportId, "slot.0")).Succeeded,
                Is.True);

            view.Show(session.ActivePreview);

            Assert.That(view.CurrentFootprint, Is.Not.Null);
            Assert.That(view.CurrentGhost, Is.Not.Null);
            Assert.That(view.CurrentFootprint, Is.Not.SameAs(prefab));
            Assert.That(view.CurrentFootprint.transform.parent, Is.SameAs(ownedRoot.transform));
            Assert.That(view.CurrentGhost.transform.parent, Is.SameAs(ownedRoot.transform));
            AssertDirectActiveChildren(
                ownedRoot.transform,
                view.CurrentFootprint,
                view.CurrentGhost);
        }

        [Test]
        public void ProductionPickUpPointIndicatorPrefab_IsPersistentExactGeometryAndUsableByRuntimeView()
        {
            var prefab = LoadEditorAsset<GameObject>(PickUpIndicatorPrefabPath);

            Assert.That(prefab, Is.Not.Null, PickUpIndicatorPrefabPath);
            var footprint = AssertRenderChild(prefab.transform, "Footprint");
            var pyramid = AssertRenderChild(prefab.transform, "InvertedSquarePyramid");
            Assert.That(footprint, Is.Not.SameAs(pyramid));
            AssertExactOneByOneFootprint(footprint);
            Assert.That(pyramid.GetComponent<MeshFilter>().sharedMesh.vertexCount, Is.EqualTo(4));
            Assert.That(pyramid.GetComponent<Renderer>().sharedMaterial.GetTexture("_BaseMap"), Is.Not.Null);
            Assert.That(pyramid.GetComponent<AnimalCafe.UI.P8R.P8RPickUpSignBillboard>(), Is.Not.Null);
            AssertNoPhysicsOrNavigationComponents(prefab);

            var fixture = CreateFunctionalFixture();
            var ownedRoot = CreateObject("ProductionPickUpOwnedRoot");
            var view = CreateObject("ProductionPickUpIndicatorView")
                .AddComponent<PickUpPointIndicatorView>();
            view.Configure(
                fixture.FurnitureRegistry,
                ownedRoot.transform,
                prefab,
                theme);
            view.Rebuild(fixture.FunctionalLayout.PickUpPoints, true);
            var session = new FunctionalSurfaceDecorationSession(fixture.FunctionalLayout);
            Assert.That(session.BeginCreatePickUp(
                new SurfaceSlotAddress(SupportId, "slot.0")).Succeeded,
                Is.True);

            view.ShowPreview(session.ActivePreview);

            Assert.That(view.CurrentPreview, Is.Not.Null);
            Assert.That(view.CurrentPreview, Is.Not.SameAs(prefab));
            Assert.That(view.CurrentPreview.transform.parent, Is.SameAs(ownedRoot.transform));
            AssertDirectActiveChildren(ownedRoot.transform, view.CurrentPreview);
            AssertIndicatorGeometry(view.CurrentPreview);
        }
#endif

        [Test]
        public void InteractionAnchorDebugView_RequiresExplicitDebugVisibilityAndMatchesConfirmedReport()
        {
            var runtimeFixture = CreateRuntimeFixture(Vector3.up * 0.72f);
            runtimeFixture.Runtime.Initialize();
            Assert.That(runtimeFixture.Runtime.FunctionalSurfaceLayout.PlacePickUp(
                new PickUpPointInstance(
                    PickUpIdA,
                    new SurfaceSlotAddress(SupportId, "slot.0"))).Succeeded,
                Is.True);
            runtimeFixture.Runtime.RecalculateReadiness();
            var report = runtimeFixture.Runtime.CurrentReadiness;
            var anchors = report.Stations.Single().Anchors.Anchors;
            var root = CreateObject("AnchorDebugRoot");
            var view = CreateObject("InteractionAnchorDebugView")
                .AddComponent<InteractionAnchorDebugView>();
            var employeeMaterial = CreateMaterial();
            var customerMaterial = CreateMaterial();
            view.Configure(
                root.transform,
                new DecorationGridSpace(
                    runtimeFixture.Runtime.Layout.GridSettings,
                    new LayoutBounds(new GridPosition(0, 0), new GridSize(8, 8))),
                employeeMaterial,
                customerMaterial);

            view.Rebuild(report, false);
            Assert.That(view.VisibleAnchorCount, Is.Zero,
                "Normal MainCafe state must not show developer anchor visuals.");

            view.Rebuild(report, true);
            Assert.That(view.VisibleAnchorCount, Is.EqualTo(anchors.Count));
            var actualPositions = root.GetComponentsInChildren<MeshRenderer>(false)
                .Select(renderer => renderer.transform.position)
                .ToArray();
            foreach (var anchor in anchors)
            {
                var expected = view.transform.TransformPoint(
                    new DecorationGridSpace(
                        runtimeFixture.Runtime.Layout.GridSettings,
                        new LayoutBounds(new GridPosition(0, 0), new GridSize(8, 8)))
                    .GetCellCenterLocal(anchor.Position, 0.08f));
                Assert.That(actualPositions.Any(position =>
                    Vector3.Distance(position, expected) < Epsilon), Is.True);
            }

            Assert.That(runtimeFixture.Runtime.Layout.MoveFurniture(
                SupportId,
                new GridPosition(5, 4)).Succeeded,
                Is.True);
            Assert.That(runtimeFixture.Runtime.Layout.RotateFurniture(
                SupportId,
                FurnitureRotation.Degrees90).Succeeded,
                Is.True);
            runtimeFixture.Runtime.RecalculateReadiness();
            var movedAnchors = runtimeFixture.Runtime.CurrentReadiness
                .Stations.Single().Anchors.Anchors;
            view.Rebuild(runtimeFixture.Runtime.CurrentReadiness, true);
            var movedPositions = root.GetComponentsInChildren<MeshRenderer>(false)
                .Select(renderer => renderer.transform.position)
                .ToArray();
            Assert.That(movedPositions.All(position => actualPositions.All(previous =>
                Vector3.Distance(position, previous) > Epsilon)), Is.True,
                "Fresh confirmed readiness and debug markers must follow support Move/Rotate.");
            foreach (var anchor in movedAnchors)
            {
                var expected = new DecorationGridSpace(
                        runtimeFixture.Runtime.Layout.GridSettings,
                        new LayoutBounds(new GridPosition(0, 0), new GridSize(8, 8)))
                    .GetCellCenterLocal(anchor.Position, 0.08f);
                Assert.That(movedPositions.Any(position =>
                    Vector3.Distance(position, expected) < Epsilon), Is.True);
            }

            view.Rebuild(runtimeFixture.Runtime.CurrentReadiness, false);
            Assert.That(view.VisibleAnchorCount, Is.Zero);
        }

        private RuntimeFixture CreateRuntimeFixture(Vector3 markerPosition)
        {
            var supportPrefab = CreateSupportPrefab(
                "RuntimeSupportPrefab",
                1,
                1,
                ("slot.0", markerPosition));
            var supportDefinition = CreateDefinition(
                SupportDefinitionId,
                1,
                1,
                PlacementSurfaceType.Floor,
                FurnitureFunctionType.None,
                supportPrefab);
            var catalog = CreateContentCatalog(supportDefinition);
            var entrance = CreateObject("Task6Entrance").AddComponent<EntrancePortalAuthoring>();
            SetField(entrance, "entranceId", "entrance.main");
            SetField(entrance, "originX", 3);
            SetField(entrance, "originY", 0);
            var runtime = CreateObject("Task6CafeLayoutRuntime").AddComponent<CafeLayoutRuntime>();
            SetField(runtime, "contentCatalog", catalog);
            SetField(runtime, "entrancePortal", entrance);
            return new RuntimeFixture(
                runtime,
                supportPrefab.GetComponentInChildren<SurfaceSlotMarker>(true));
        }

        private FunctionalFixture CreateFunctionalFixture(
            string mountedDefinitionId = MountedDefinitionId,
            FurnitureFunctionType mountedFunctionType = FurnitureFunctionType.CashRegister)
        {
            var supportPrefab = CreateSupportPrefab(
                "Task6SupportPrefab",
                1,
                4,
                ("slot.0", new Vector3(0f, 0.72f, -1.5f)),
                ("slot.1", new Vector3(0f, 0.72f, -0.5f)),
                ("slot.2", new Vector3(0f, 0.72f, 0.5f)),
                ("slot.3", new Vector3(0f, 0.72f, 1.5f)));
            var equipmentPrefab = CreateVisualPrefab("Task6MountedPrefab");
            var supportDefinition = CreateDefinition(
                SupportDefinitionId,
                1,
                4,
                PlacementSurfaceType.Floor,
                FurnitureFunctionType.None,
                supportPrefab);
            var mountedDefinition = CreateDefinition(
                mountedDefinitionId,
                1,
                1,
                PlacementSurfaceType.FurnitureSurface,
                mountedFunctionType,
                equipmentPrefab);
            var catalog = CreateContentCatalog(supportDefinition, mountedDefinition);
            var definitions = catalog.BuildRuntimeCatalog();
            var settings = new GridSettings(1f);
            var bounds = new LayoutBounds(new GridPosition(0, 0), new GridSize(8, 8));
            var layout = new CafeLayout(settings, definitions, bounds);
            layout.AddRegion(new LayoutRegion(
                "region.main",
                bounds.Origin,
                bounds.Size,
                LayoutZoneType.Interior));
            Assert.That(layout.PlaceFurniture(FurnitureInstance.Restore(
                SupportId,
                SupportDefinitionId,
                new GridPosition(1, 2),
                FurnitureRotation.Degrees0)).Succeeded,
                Is.True);
            var functionalLayout = new FunctionalSurfaceLayout(
                layout,
                definitions,
                catalog.BuildSurfaceSlotCatalog(settings));
            var representationRoot = CreateObject("Task6FurnitureRepresentationRoot");
            var furnitureRegistry = CreateObject("Task6FurnitureRegistry")
                .AddComponent<FurnitureSceneRegistry>();
            furnitureRegistry.Configure(
                catalog,
                representationRoot.transform,
                new DecorationGridSpace(settings, bounds));
            furnitureRegistry.Rebuild(layout.FurnitureInstances);
            Assert.That(furnitureRegistry.TryGet(SupportId, out _), Is.True);
            return new FunctionalFixture(
                catalog,
                layout,
                functionalLayout,
                furnitureRegistry);
        }

        private GameObject CreateSupportPrefab(
            string name,
            int width,
            int depth,
            params (string slotId, Vector3 localPosition)[] slots)
        {
            var root = CreateVisualPrefab(name);
            foreach (var slot in slots)
            {
                var markerObject = CreateObject("MisleadingMarkerName");
                markerObject.transform.SetParent(root.transform, false);
                markerObject.transform.localPosition = slot.localPosition;
                var marker = markerObject.AddComponent<SurfaceSlotMarker>();
                SetField(marker, "slotId", slot.slotId);
            }

            root.SetActive(false);
            return root;
        }

        private GameObject CreateVisualPrefab(string name)
        {
            var root = CreateObject(name);
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            owned.Add(visual);
            visual.name = "Visual";
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = Vector3.up * 0.3f;
            visual.transform.localScale = new Vector3(0.65f, 0.6f, 0.65f);
            visual.GetComponent<Renderer>().sharedMaterial = material;
            root.SetActive(false);
            return root;
        }

        private FurnitureDefinitionAsset CreateDefinition(
            string definitionId,
            int width,
            int depth,
            PlacementSurfaceType surfaces,
            FurnitureFunctionType functionType,
            GameObject prefab)
        {
            var definition = ScriptableObject.CreateInstance<FurnitureDefinitionAsset>();
            owned.Add(definition);
            SetField(definition, "definitionId", definitionId);
            SetField(definition, "displayName", definitionId);
            SetField(definition, "footprintWidth", width);
            SetField(definition, "footprintDepth", depth);
            SetField(definition, "allowedPlacementSurfaces", surfaces);
            SetField(definition, "functionType", functionType);
            SetField(definition, "prefab", prefab);
            return definition;
        }

        private FurnitureContentCatalog CreateContentCatalog(
            params FurnitureDefinitionAsset[] definitions)
        {
            var catalog = ScriptableObject.CreateInstance<FurnitureContentCatalog>();
            owned.Add(catalog);
            SetField(catalog, "entries", definitions.ToList());
            return catalog;
        }

        private GameObject CreateFunctionalPreviewTemplate()
        {
            var root = CreateObject("PF_UI_FunctionalSurfacePreview_TestTemplate");
            var footprint = GameObject.CreatePrimitive(PrimitiveType.Cube);
            owned.Add(footprint);
            footprint.name = "Footprint";
            footprint.transform.SetParent(root.transform, false);
            footprint.transform.localPosition = Vector3.up * 0.02f;
            footprint.transform.localScale = new Vector3(1f, 0.04f, 1f);
            footprint.GetComponent<Renderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(footprint.GetComponent<Collider>());
            root.SetActive(false);
            return root;
        }

        private GameObject CreatePickUpIndicatorTemplate()
        {
            var root = CreateObject("PF_UI_PickUpPointIndicator_TestTemplate");
            var footprint = GameObject.CreatePrimitive(PrimitiveType.Cube);
            owned.Add(footprint);
            footprint.name = "Footprint";
            footprint.transform.SetParent(root.transform, false);
            footprint.transform.localPosition = Vector3.up * 0.02f;
            footprint.transform.localScale = new Vector3(1f, 0.04f, 1f);
            footprint.GetComponent<Renderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(footprint.GetComponent<Collider>());

            var pyramid = CreateObject("InvertedSquarePyramid");
            pyramid.transform.SetParent(root.transform, false);
            var mesh = CreateInvertedSquarePyramidMesh();
            owned.Add(mesh);
            pyramid.AddComponent<MeshFilter>().sharedMesh = mesh;
            var pyramidMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            owned.Add(pyramidMaterial);
            pyramidMaterial.SetColor("_BaseColor", new Color(.82f, .82f, .82f, 1f));
            pyramid.AddComponent<MeshRenderer>().sharedMaterial = pyramidMaterial;
            root.SetActive(false);
            return root;
        }

        private static Mesh CreateInvertedSquarePyramidMesh()
        {
            var mesh = new Mesh { name = "Task6InvertedSquarePyramid" };
            mesh.vertices = new[]
            {
                new Vector3(-0.28f, 0.62f, -0.28f),
                new Vector3(0.28f, 0.62f, -0.28f),
                new Vector3(0.28f, 0.62f, 0.28f),
                new Vector3(-0.28f, 0.62f, 0.28f),
                new Vector3(0f, 0.12f, 0f)
            };
            mesh.triangles = new[]
            {
                0, 1, 4, 1, 2, 4, 2, 3, 4, 3, 0, 4,
                0, 3, 2, 0, 2, 1
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private Vector3 SlotWorldPosition(FunctionalFixture fixture, string slotId)
        {
            Assert.That(fixture.FurnitureRegistry.TryGet(
                SupportId,
                out var supportRepresentation), Is.True);
            fixture.SupportRepresentation = supportRepresentation;
            var markers = supportRepresentation.GetComponentsInChildren<SurfaceSlotMarker>(true)
                .Where(marker => string.Equals(marker.SlotId, slotId, StringComparison.Ordinal))
                .ToArray();
            Assert.That(markers, Has.Length.EqualTo(1));
            return markers[0].transform.position;
        }

        private static Renderer[] ActiveRenderers(GameObject target)
        {
            return target.GetComponentsInChildren<Renderer>(false);
        }

        private static void AssertDirectActiveChildren(
            Transform root,
            params GameObject[] expectedChildren)
        {
            var actualChildren = Enumerable.Range(0, root.childCount)
                .Select(root.GetChild)
                .Where(child => child.gameObject.activeSelf)
                .Select(child => child.gameObject)
                .ToArray();
            AssertSameDirectChildren(
                actualChildren,
                expectedChildren,
                "Only the current owned representations may remain active immediately.");
        }

        private static void AssertDirectChildren(
            Transform root,
            params GameObject[] expectedChildren)
        {
            var actualChildren = Enumerable.Range(0, root.childCount)
                .Select(root.GetChild)
                .Select(child => child.gameObject)
                .ToArray();
            AssertSameDirectChildren(
                actualChildren,
                expectedChildren,
                "Deferred destruction must leave only the expected owned representations next frame.");
        }

        private static void AssertSameDirectChildren(
            IReadOnlyList<GameObject> actualChildren,
            IReadOnlyList<GameObject> expectedChildren,
            string message)
        {
            Assert.That(actualChildren.Count, Is.EqualTo(expectedChildren.Count), message);
            foreach (var expected in expectedChildren)
            {
                Assert.That(
                    actualChildren.Any(actual => ReferenceEquals(actual, expected)),
                    Is.True,
                    message);
            }
        }

#if UNITY_EDITOR
        private static T LoadEditorAsset<T>(string path) where T : UnityEngine.Object
        {
            var assetDatabase = Type.GetType("UnityEditor.AssetDatabase, UnityEditor.CoreModule");
            Assert.That(assetDatabase, Is.Not.Null,
                "Production prefab tests require the Unity Editor PlayMode runner.");
            var load = assetDatabase.GetMethod(
                "LoadAssetAtPath",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(string), typeof(Type) },
                null);
            Assert.That(load, Is.Not.Null,
                "UnityEditor.AssetDatabase.LoadAssetAtPath(string, Type) was not found.");
            var asset = load.Invoke(null, new object[] { path, typeof(T) });
            Assert.That(asset, Is.InstanceOf<T>(),
                $"Asset '{path}' must be '{typeof(T).FullName}', but was " +
                $"'{asset?.GetType().FullName ?? "null"}'.");
            return (T)asset;
        }

        private static Transform AssertRenderChild(Transform root, string childName)
        {
            var matches = Enumerable.Range(0, root.childCount)
                .Select(root.GetChild)
                .Where(child => string.Equals(child.name, childName, StringComparison.Ordinal))
                .ToArray();
            Assert.That(matches, Has.Length.EqualTo(1),
                $"'{root.name}' must own one direct '{childName}' render child.");
            var child = matches[0];
            var filter = child.GetComponent<MeshFilter>();
            Assert.That(filter, Is.Not.Null, childName);
            Assert.That(filter.sharedMesh, Is.Not.Null, childName);
            Assert.That(child.GetComponent<MeshRenderer>(), Is.Not.Null, childName);
            return child;
        }

        private static void AssertExactOneByOneFootprint(Transform footprint)
        {
            // Measure the occupied parent-space surface, including the flat Quad's rotation.
            // 将真实顶点转换到父级空间，保留旋转后严格的 1 x 1 占用范围检查。
            var points = footprint.GetComponent<MeshFilter>().sharedMesh.vertices
                .Select(vertex => footprint.parent.InverseTransformPoint(footprint.TransformPoint(vertex)))
                .ToArray();
            Assert.That(points, Is.Not.Empty);
            Assert.That(points.Max(point => point.x) - points.Min(point => point.x),
                Is.EqualTo(1f).Within(Epsilon));
            Assert.That(points.Max(point => point.z) - points.Min(point => point.z),
                Is.EqualTo(1f).Within(Epsilon));
        }

        private static void AssertInvertedSquarePyramid(
            Transform pyramid,
            Transform footprint,
            Transform prefabRoot)
        {
            var mesh = pyramid.GetComponent<MeshFilter>().sharedMesh;
            var vertices = mesh.vertices.Distinct().ToArray();
            Assert.That(vertices, Has.Length.EqualTo(5));
            var tipY = vertices.Min(vertex => vertex.y);
            var tipCandidates = vertices
                .Where(vertex => Mathf.Abs(vertex.y - tipY) <= Epsilon)
                .ToArray();
            Assert.That(tipCandidates, Has.Length.EqualTo(1),
                "The five distinct positions must describe one downward tip.");
            var tip = tipCandidates[0];
            var baseVertices = vertices.Where(vertex => vertex.y > tipY + Epsilon).ToArray();
            Assert.That(baseVertices, Has.Length.EqualTo(4));
            Assert.That(baseVertices.All(vertex => vertex.y > tip.y), Is.True);
            Assert.That(baseVertices.Max(vertex => vertex.y) -
                        baseVertices.Min(vertex => vertex.y),
                Is.LessThanOrEqualTo(Epsilon),
                "All four base vertices must share a plane above the tip.");
            Assert.That(tip.x, Is.EqualTo(baseVertices.Average(vertex => vertex.x))
                .Within(Epsilon));
            Assert.That(tip.z, Is.EqualTo(baseVertices.Average(vertex => vertex.z))
                .Within(Epsilon));

            var actualSize = Vector3.Scale(mesh.bounds.size, Absolute(pyramid.lossyScale));
            Assert.That(actualSize.x, Is.EqualTo(.28f).Within(.01f),
                "The silhouette must be half the old .56 m width.");
            Assert.That(actualSize.z, Is.EqualTo(.28f).Within(.01f));
            Assert.That(actualSize.y, Is.EqualTo(.5f).Within(.01f));
            AssertFlatOutwardFaceNormals(mesh);

            var rootLocalTip = prefabRoot.InverseTransformPoint(pyramid.TransformPoint(tip));
            var footprintMesh = footprint.GetComponent<MeshFilter>().sharedMesh;
            var rootLocalFootprintTop = prefabRoot.InverseTransformPoint(
                footprint.TransformPoint(new Vector3(0f, footprintMesh.bounds.max.y, 0f)));
            Assert.That(rootLocalTip.y - rootLocalFootprintTop.y,
                Is.GreaterThanOrEqualTo(.15f),
                "The raised tip needs visible clearance above the footprint.");
        }

        private static void AssertFlatOutwardFaceNormals(Mesh mesh)
        {
            var vertices = mesh.vertices;
            var normals = mesh.normals;
            var triangles = mesh.triangles;
            Assert.That(triangles, Has.Length.EqualTo(18));
            Assert.That(normals, Has.Length.EqualTo(vertices.Length));
            for (var index = 0; index < triangles.Length; index += 3)
            {
                var a = triangles[index];
                var b = triangles[index + 1];
                var c = triangles[index + 2];
                var faceNormal = Vector3.Cross(
                    vertices[b] - vertices[a], vertices[c] - vertices[a]).normalized;
                Assert.That(faceNormal.sqrMagnitude, Is.GreaterThan(.99f));
                foreach (var vertexIndex in new[] { a, b, c })
                {
                    Assert.That(Vector3.Dot(normals[vertexIndex], faceNormal),
                        Is.GreaterThan(.999f), "Each face must keep its own hard normal.");
                }
                var faceCenter = (vertices[a] + vertices[b] + vertices[c]) / 3f;
                Assert.That(Vector3.Dot(faceNormal, faceCenter - mesh.bounds.center),
                    Is.GreaterThan(0f), "Faces must point outward for back-face culling.");
            }
        }

        private static void AssertNeutralLitPyramid(Transform pyramid, Transform footprint)
        {
            var pyramidMaterial = pyramid.GetComponent<Renderer>().sharedMaterial;
            Assert.That(pyramidMaterial, Is.Not.Null);
            Assert.That(pyramidMaterial,
                Is.Not.SameAs(footprint.GetComponent<Renderer>().sharedMaterial));
            Assert.That(pyramidMaterial.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"));
            Assert.That(pyramidMaterial.GetFloat("_Surface"), Is.Zero);
            var color = pyramidMaterial.GetColor("_BaseColor");
            Assert.That(color.r, Is.InRange(.75f, .95f));
            Assert.That(color.g, Is.EqualTo(color.r).Within(.02f));
            Assert.That(color.b, Is.EqualTo(color.r).Within(.02f));
            Assert.That(color.a, Is.EqualTo(1f).Within(Epsilon));
        }

        private static void AssertNoPhysicsOrNavigationComponents(GameObject prefab)
        {
            var forbidden = prefab.GetComponentsInChildren<Component>(true)
                .Where(component => component != null &&
                                    (component is Collider ||
                                     component is Rigidbody ||
                                     string.Equals(
                                         component.GetType().Name,
                                         "NavMeshObstacle",
                                         StringComparison.Ordinal)))
                .Select(component => component.GetType().Name)
                .ToArray();
            Assert.That(forbidden, Is.Empty,
                "Preview prefabs must remain render-only and non-interactive.");
        }

        private static Vector3 Absolute(Vector3 value)
        {
            return new Vector3(
                Mathf.Abs(value.x),
                Mathf.Abs(value.y),
                Mathf.Abs(value.z));
        }
#endif

        private static void AssertIndicatorGeometry(GameObject indicator)
        {
            Assert.That(indicator, Is.Not.Null);
            var footprint = indicator.transform.Find("Footprint");
            var pyramid = indicator.transform.Find("InvertedSquarePyramid");
            Assert.That(footprint, Is.Not.Null);
            Assert.That(pyramid, Is.Not.Null);
            Assert.That(footprint.GetComponent<MeshRenderer>(), Is.Not.Null);
            var mesh = pyramid.GetComponent<MeshFilter>().sharedMesh;
            Assert.That(mesh, Is.Not.Null);
            Assert.That(mesh.vertices.Distinct().Count(), Is.EqualTo(
                pyramid.GetComponent<AnimalCafe.UI.P8R.P8RPickUpSignBillboard>() != null ? 4 : 5),
                "Production sign is a quad; isolated legacy fixtures still exercise generic view ownership.");
            Assert.That(mesh.vertices.Min(vertex => vertex.y),
                Is.LessThan(mesh.vertices.Max(vertex => vertex.y)),
                "The pyramid tip must point down toward the separate footprint.");
            Assert.That(indicator.GetComponentsInChildren<Collider>(true), Is.Empty);
        }

        private void AssertAuthoredModelAppearance(GameObject model, Color expectedColor, Texture expectedTexture)
        {
            var renderers = model.GetComponentsInChildren<Renderer>(true);
            Assert.That(renderers, Is.Not.Empty);
            foreach (var renderer in renderers)
            {
                Assert.That(renderer.sharedMaterial, Is.SameAs(material));
                var actualColor = renderer.sharedMaterial.color;
                Assert.That(actualColor.r, Is.EqualTo(expectedColor.r).Within(Epsilon));
                Assert.That(actualColor.g, Is.EqualTo(expectedColor.g).Within(Epsilon));
                Assert.That(actualColor.b, Is.EqualTo(expectedColor.b).Within(Epsilon));
                Assert.That(actualColor.a, Is.EqualTo(expectedColor.a).Within(Epsilon));
                Assert.That(renderer.sharedMaterial.mainTexture, Is.SameAs(expectedTexture));
                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                Assert.That(block.isEmpty, Is.True,
                    "Slot validity must color only the footprint and preserve the model's authored appearance.");
            }
        }

        private static void AssertColor(Renderer renderer, Color expected)
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

        private static void AssertPickUpStateColor(GameObject indicator, Color expected)
        {
            AssertColor(indicator.transform.Find("Footprint").GetComponent<Renderer>(), expected);
            var pyramid = indicator.transform.Find("InvertedSquarePyramid").GetComponent<Renderer>();
            var block = new MaterialPropertyBlock();
            pyramid.GetPropertyBlock(block);
            Assert.That(block.HasColor(Shader.PropertyToID("_BaseColor")), Is.False,
                "State feedback must not replace the pyramid's authored neutral color.");
            Assert.That(block.HasColor(Shader.PropertyToID("_Color")), Is.False);
            var color = pyramid.sharedMaterial.GetColor("_BaseColor");
            Assert.That(color.r, Is.InRange(.75f, .95f));
            Assert.That(color.g, Is.EqualTo(color.r).Within(.02f));
            Assert.That(color.b, Is.EqualTo(color.r).Within(.02f));
        }

        private static void AssertVector(Vector3 actual, Vector3 expected)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(Epsilon));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(Epsilon));
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(Epsilon));
        }

        private Material CreateMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ??
                         Shader.Find("Universal Render Pipeline/Lit") ??
                         Shader.Find("Sprites/Default");
            Assert.That(shader, Is.Not.Null);
            var created = new Material(shader);
            owned.Add(created);
            return created;
        }

        private GameObject CreateObject(string name)
        {
            var created = new GameObject(name);
            owned.Add(created);
            return created;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field '{fieldName}'.");
            field.SetValue(target, value);
        }

        private sealed class RuntimeFixture
        {
            public RuntimeFixture(CafeLayoutRuntime runtime, SurfaceSlotMarker marker)
            {
                Runtime = runtime;
                Marker = marker;
            }

            public CafeLayoutRuntime Runtime { get; }
            public SurfaceSlotMarker Marker { get; }
        }

        private sealed class FunctionalFixture
        {
            public FunctionalFixture(
                FurnitureContentCatalog catalog,
                CafeLayout layout,
                FunctionalSurfaceLayout functionalLayout,
                FurnitureSceneRegistry furnitureRegistry)
            {
                Catalog = catalog;
                Layout = layout;
                FunctionalLayout = functionalLayout;
                FurnitureRegistry = furnitureRegistry;
                Assert.That(furnitureRegistry.TryGet(SupportId, out var representation), Is.True);
                SupportRepresentation = representation;
            }

            public FurnitureContentCatalog Catalog { get; }
            public CafeLayout Layout { get; }
            public FunctionalSurfaceLayout FunctionalLayout { get; }
            public FurnitureSceneRegistry FurnitureRegistry { get; }
            public GameObject SupportRepresentation { get; set; }
        }
    }
}

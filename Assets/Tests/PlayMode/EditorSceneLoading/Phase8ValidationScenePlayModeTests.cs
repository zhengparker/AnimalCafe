#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using AnimalCafe.Decoration;
using AnimalCafe.Layout;
using AnimalCafe.UI.Decoration;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace AnimalCafe.Tests.PlayMode.EditorSceneLoading
{
    public sealed class Phase8ValidationScenePlayModeTests
    {
        private const string ScenePath =
            "Assets/Scenes/Validation/Phase8FunctionalFurniture.unity";

        [UnityTearDown]
        public IEnumerator RestoreCleanScene()
        {
            Time.timeScale = 1f;
            var active = SceneManager.GetActiveScene();
            var cleanup = SceneManager.CreateScene("Phase8ValidationCleanup");
            SceneManager.SetActiveScene(cleanup);
            if (active.IsValid() && active.isLoaded && active != cleanup)
            {
                var unload = SceneManager.UnloadSceneAsync(active);
                while (unload != null && !unload.isDone) yield return null;
            }
        }

        [UnityTest]
        public IEnumerator FreshLoad_ExposesExplicitDebugAndReviewableFourRotationControllerFlow()
        {
            EditorSceneManager.LoadSceneInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            yield return null;
            yield return null;

            var controller = Object.FindFirstObjectByType<DecorationModeController>();
            var debug = Object.FindObjectsByType<InteractionAnchorDebugView>(
                FindObjectsInactive.Include, FindObjectsSortMode.None).Single();
            Assert.That(controller, Is.Not.Null);
            Assert.That(debug, Is.Not.Null);
            controller.EnterDecorationMode();
            yield return null;

            var catalogue = Object.FindFirstObjectByType<DecorationCatalogueView>();
            Assert.That(catalogue.CategoryRows.Select(row => row.HorizontalScroll.content.childCount),
                Is.EqualTo(new[] { 4, 1, 1 }));
            var cashRegister = catalogue.GetComponentsInChildren<DecorationCatalogueTileView>(true)
                .Single(tile => tile.ItemId == "equipment.cash-register.01");
            cashRegister.GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.That(controller.ActiveFunctionalSurfacePreview.CanConfirm, Is.True);
            var observed = new[]
            {
                controller.ActiveFunctionalSurfacePreview.Rotation,
                Rotate(controller),
                Rotate(controller),
                Rotate(controller)
            };
            Assert.That(observed, Is.EquivalentTo(new[]
            {
                FurnitureRotation.Degrees0,
                FurnitureRotation.Degrees90,
                FurnitureRotation.Degrees180,
                FurnitureRotation.Degrees270
            }));
            Assert.That(controller.TryRotateFunctionalSurfacePreview(), Is.True);
            Assert.That(controller.ActiveFunctionalSurfacePreview.Rotation,
                Is.EqualTo(FurnitureRotation.Degrees0));
            Assert.That(controller.TryConfirmFunctionalSurfacePreview(), Is.True);
            yield return null;
            Assert.That(Object.FindFirstObjectByType<CafeLayoutRuntime>()
                .FunctionalSurfaceLayout.MountedInstances, Has.Count.EqualTo(1));
            Assert.That(Object.FindFirstObjectByType<SurfaceMountedSceneRegistry>(), Is.Not.Null);
            Assert.That(debug.VisibleAnchorCount, Is.GreaterThan(0));
        }

        [UnityTest]
        public IEnumerator PickUpButton_IsDecorationOnlyAndStartsOneSlotPreview()
        {
            EditorSceneManager.LoadSceneInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            yield return null;
            var controller = Object.FindFirstObjectByType<DecorationModeController>();
            var indicators = Object.FindFirstObjectByType<PickUpPointIndicatorView>();
            Assert.That(controller.IsOpen, Is.False);
            Assert.That(Object.FindObjectsByType<Transform>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Any(item => item.name.StartsWith("PickUpPoint_")), Is.False);

            controller.EnterDecorationMode();
            yield return null;
            var catalogue = Object.FindFirstObjectByType<DecorationCatalogueView>();
            catalogue.GetComponentsInChildren<Button>(true)
                .Single(button => button.name == "PickUpPointButton")
                .onClick.Invoke();
            yield return null;
            Assert.That(controller.ActiveFunctionalSurfacePreview, Is.Not.Null);
            Assert.That(controller.ActiveFunctionalSurfacePreview.Kind,
                Is.EqualTo(FunctionalSurfacePreviewKind.PickUpPoint));
            Assert.That(indicators.CurrentPreview, Is.Not.Null);
            controller.CancelFunctionalSurfacePreview();
            controller.ExitDecorationMode();
            yield return null;
            Assert.That(indicators.CurrentPreview, Is.Null);
            Assert.That(Object.FindObjectsByType<Transform>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Any(item => item.name.StartsWith("PickUpPoint_")), Is.False);
        }

        // M18 uses the real scenes/controllers; fixture setup never saves assets or scenes.
        // 手工推导的格子/朝向独立于 resolver，避免用被测代码计算自己的预期结果。
        private CafeLayoutRuntime m18Runtime;
        private DecorationModeController m18Controller;
        private FurnitureInstance[] m18Supports;
        private static readonly GridPosition[] M18SupportCells =
        {
            new GridPosition(2, 3), new GridPosition(4, 3), new GridPosition(6, 3)
        };

        [UnityTest]
        public IEnumerator M18_ConfirmedRotationsAndSupportMovesKeepExactUniqueMarkers()
        {
            yield return PrepareM18();
            var expected = M18Baseline();
            var probe = System.Environment.GetEnvironmentVariable("ANIMALCAFE_P8_M18_NEGATIVE_PROBE");
            var restore = string.IsNullOrEmpty(probe) ? null : InjectM18MarkerFault(probe);
            try { AssertM18Markers(expected); }
            finally { restore?.Invoke(); }

            // Literal direction table: East, South, West, North after successive quarter turns.
            var offsets = new[]
            {
                new GridPosition(1, 0), new GridPosition(0, -1),
                new GridPosition(-1, 0), new GridPosition(0, 1)
            };
            var facings = new[]
            {
                CardinalDirection.West, CardinalDirection.North,
                CardinalDirection.East, CardinalDirection.South
            };
            var customerFacings = new[]
            {
                CardinalDirection.East, CardinalDirection.South,
                CardinalDirection.West, CardinalDirection.North
            };
            for (var device = 0; device < 2; device++)
            {
                var type = device == 0 ? LayoutStationType.CashRegister : LayoutStationType.CoffeeMachine;
                var station = m18Runtime.CurrentReadiness.Stations.Single(item => item.FunctionType == type);
                for (var turn = 0; turn < 4; turn++)
                {
                    var previousVersion = m18Runtime.ReadinessVersion;
                    Assert.That(m18Controller.TryBeginExistingFunctionalSurfacePreview(
                        FunctionalSurfacePreviewKind.MountedEquipment, station.InstanceId), Is.True);
                    Assert.That(m18Controller.TryRotateFunctionalSurfacePreview(), Is.True);
                    yield return null;
                    AssertM18Markers(expected);
                    Assert.That(m18Runtime.ReadinessVersion, Is.EqualTo(previousVersion));
                    m18Controller.CancelFunctionalSurfacePreview();
                    yield return null;
                    AssertM18Markers(expected);

                    Assert.That(m18Controller.TryBeginExistingFunctionalSurfacePreview(
                        FunctionalSurfacePreviewKind.MountedEquipment, station.InstanceId), Is.True);
                    Assert.That(m18Controller.TryRotateFunctionalSurfacePreview(), Is.True);
                    ClickM18Action("ConfirmButton");
                    yield return null;
                    yield return null;
                    Assert.That(m18Controller.ActiveFunctionalSurfacePreview, Is.Null);
                    Assert.That(m18Runtime.ReadinessVersion, Is.GreaterThan(previousVersion));
                    var center = M18SupportCells[device];
                    var offset = offsets[turn];
                    expected = expected.Select(anchor => anchor.Type != type ? anchor :
                        new M18ExpectedAnchor(type, anchor.Role,
                            center.X + (anchor.Role == InteractionRole.Employee ? offset.X : -offset.X),
                            center.Y + (anchor.Role == InteractionRole.Employee ? offset.Y : -offset.Y),
                            anchor.Role == InteractionRole.Employee ? facings[turn] : customerFacings[turn]))
                        .ToArray();
                    AssertM18Markers(expected);
                }
            }

            // Counter has no public begin/move entry; invoke its existing input handlers,
            // then use the real action bar. Never call debug.Rebuild or recalculate by hand.
            for (var index = 0; index < 3; index++)
            {
                var type = (LayoutStationType)index;
                var target = new GridPosition(M18SupportCells[index].X, 5);
                var previousVersion = m18Runtime.ReadinessVersion;
                InvokeM18Controller("HandleFurnitureBegan", m18Supports[index].InstanceId);
                InvokeM18Controller("ApplyPreviewMove", target);
                ClickM18Action("RotateButton");
                yield return null;
                AssertM18Markers(expected);
                Assert.That(m18Runtime.ReadinessVersion, Is.EqualTo(previousVersion));
                m18Controller.CancelActivePreview();
                yield return null;
                AssertM18Markers(expected);

                InvokeM18Controller("HandleFurnitureBegan", m18Supports[index].InstanceId);
                InvokeM18Controller("ApplyPreviewMove", target);
                ClickM18Action("ConfirmButton");
                yield return null;
                yield return null;
                expected = expected.Select(anchor => anchor.Type != type ? anchor :
                    new M18ExpectedAnchor(type, anchor.Role, anchor.X, anchor.Y + 2, anchor.Facing))
                    .ToArray();
                Assert.That(m18Runtime.Layout.FurnitureInstances.Single(item =>
                    item.InstanceId == m18Supports[index].InstanceId).Position, Is.EqualTo(target));
                Assert.That(m18Runtime.ReadinessVersion, Is.GreaterThan(previousVersion));
                AssertM18Markers(expected);

                var beforeRotationVersion = m18Runtime.ReadinessVersion;
                InvokeM18Controller("HandleFurnitureBegan", m18Supports[index].InstanceId);
                ClickM18Action("RotateButton");
                ClickM18Action("ConfirmButton");
                yield return null;
                yield return null;
                var confirmedSupport = m18Runtime.Layout.FurnitureInstances.Single(item =>
                    item.InstanceId == m18Supports[index].InstanceId);
                Assert.That(confirmedSupport.Position, Is.EqualTo(target));
                Assert.That(confirmedSupport.Rotation, Is.EqualTo(FurnitureRotation.Degrees90));
                Assert.That(m18Runtime.ReadinessVersion, Is.GreaterThan(beforeRotationVersion));
                Assert.That(Object.FindFirstObjectByType<FurniturePreviewView>().CurrentPreviewTransform, Is.Null);
                if (type != LayoutStationType.PickUpPoint)
                {
                    expected = expected.Select(anchor => anchor.Type != type ? anchor :
                        new M18ExpectedAnchor(type, anchor.Role,
                            target.X + (anchor.Role == InteractionRole.Employee ? 1 : -1), target.Y,
                            anchor.Role == InteractionRole.Employee ? CardinalDirection.West : CardinalDirection.East))
                        .ToArray();
                }
                // A one-slot Pick-up still prefers the free north/south pair after table rotation.
                AssertM18Markers(expected);
            }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator M18_PickUpSharedCellKeepsBothRolesWithoutRotate()
        {
            yield return PrepareM18();
            var expected = M18Baseline();
            AssertM18Markers(expected);
            // Block north/east/south with actual confirmed furniture; leave west (5,3) free.
            foreach (var cell in new[]
            {
                new GridPosition(6, 4), new GridPosition(7, 3), new GridPosition(6, 2)
            })
            {
                yield return ExpandM18Catalogue();
                SelectM18Tile("furniture.counter.module.01");
                InvokeM18Controller("ApplyPreviewMove", cell);
                ClickM18Action("ConfirmButton");
                yield return null;
                yield return null;
                Assert.That(m18Runtime.Layout.TryGetOccupant(cell, out _), Is.True);
            }
            expected = expected.Select(anchor => anchor.Type != LayoutStationType.PickUpPoint ? anchor :
                new M18ExpectedAnchor(anchor.Type, anchor.Role, 5, 3, CardinalDirection.East)).ToArray();
            AssertM18Markers(expected);
            var point = m18Runtime.FunctionalSurfaceLayout.PickUpPoints.Single();
            Assert.That(m18Controller.TryBeginExistingFunctionalSurfacePreview(
                FunctionalSurfacePreviewKind.PickUpPoint, point.InstanceId), Is.True);
            Assert.That(Object.FindFirstObjectByType<DecorationActionBarView>()
                .VisibleActionLabels, Does.Not.Contain("Rotate"));
            Assert.That(m18Controller.TryRotateFunctionalSurfacePreview(), Is.False);
            m18Controller.CancelFunctionalSurfacePreview();
            yield return null;
            AssertM18Markers(expected);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator M18_PopulatedValidationToMainCafeLeavesNoDebugObjects()
        {
            yield return PrepareM18();
            AssertM18Markers(M18Baseline());
            EditorSceneManager.LoadSceneInPlayMode(
                "Assets/Scenes/MainCafe.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            yield return null;
            Assert.That(Object.FindObjectsByType<InteractionAnchorDebugView>(
                FindObjectsInactive.Include, FindObjectsSortMode.None), Is.Empty);
            Assert.That(M18Markers(), Is.Empty);
            Object.FindFirstObjectByType<DecorationModeController>().EnterDecorationMode();
            yield return null;
            Assert.That(M18Markers(), Is.Empty, "Normal MainCafe must stay debug-free even in Decor.");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator M18_AssertionsRejectMarkerAndRootFaults()
        {
            yield return PrepareM18();
            var expected = M18Baseline();
            AssertM18Markers(expected);
            foreach (var fault in new[] { "wrong-position", "wrong-color", "missing", "duplicate", "stale", "root-offset", "root-rotation" })
            {
                // Negative controls mutate runtime views only, never shared material assets.
                var restore = InjectM18MarkerFault(fault);
                try
                {
                    Assert.Throws<AssertionException>(() => AssertM18Markers(expected),
                        "M18 assertions must detect injected " + fault);
                }
                finally { restore(); }
                AssertM18Markers(expected);
            }
            LogAssert.NoUnexpectedReceived();
        }

        private IEnumerator PrepareM18()
        {
            EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            yield return null;
            m18Runtime = Object.FindFirstObjectByType<CafeLayoutRuntime>();
            m18Controller = Object.FindFirstObjectByType<DecorationModeController>();
            Assert.That(m18Runtime, Is.Not.Null);
            Assert.That(m18Controller, Is.Not.Null);
            Assert.That(m18Runtime.Layout.GridSettings.CellSize, Is.EqualTo(1f));
            m18Supports = new[]
            {
                m18Runtime.Layout.FurnitureInstances.Single(),
                FurnitureInstance.CreateNew("furniture.counter.module.01", M18SupportCells[1], FurnitureRotation.Degrees0),
                FurnitureInstance.CreateNew("furniture.counter.module.01", M18SupportCells[2], FurnitureRotation.Degrees0)
            };
            Assert.That(m18Supports[0].Position, Is.EqualTo(M18SupportCells[0]));
            foreach (var support in m18Supports.Skip(1))
                Assert.That(m18Runtime.Layout.PlaceFurniture(support).Succeeded, Is.True);
            Object.FindFirstObjectByType<FurnitureSceneRegistry>().Rebuild(m18Runtime.Layout.FurnitureInstances);
            m18Controller.EnterDecorationMode();
            yield return null;
            for (var index = 0; index < 3; index++)
            {
                yield return ExpandM18Catalogue();
                if (index < 2)
                    SelectM18Tile(index == 0 ? "equipment.cash-register.01" : "equipment.coffee-machine.01");
                else
                    Object.FindFirstObjectByType<DecorationCatalogueView>()
                        .GetComponentsInChildren<Button>(true).Single(button => button.name == "PickUpPointButton")
                        .onClick.Invoke();
                Assert.That(m18Controller.ActiveFunctionalSurfacePreview, Is.Not.Null);
                Assert.That(m18Controller.TryMoveFunctionalSurfacePreview(
                    new SurfaceSlotAddress(m18Supports[index].InstanceId, "slot.0")), Is.True);
                if (index == 2)
                    Assert.That(Object.FindFirstObjectByType<DecorationActionBarView>().VisibleActionLabels,
                        Does.Not.Contain("Rotate"), "New Pick-up preview must have no Rotate action.");
                ClickM18Action("ConfirmButton");
                yield return null;
                yield return null;
                Assert.That(m18Controller.ActiveFunctionalSurfacePreview, Is.Null);
            }
            Assert.That(m18Runtime.CurrentReadiness.CanOpenForBusiness, Is.True);
        }

        private static IEnumerator ExpandM18Catalogue()
        {
            var catalogue = Object.FindFirstObjectByType<DecorationCatalogueView>();
            if (catalogue.IsCollapsed)
                catalogue.GetComponentsInChildren<Button>(true)
                    .Single(button => button.name == "CollapsedHandle").onClick.Invoke();
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(catalogue.IsCollapsed, Is.False);
        }

        private static void SelectM18Tile(string definitionId)
        {
            Object.FindFirstObjectByType<DecorationCatalogueView>()
                .GetComponentsInChildren<DecorationCatalogueTileView>(true)
                .Single(tile => tile.ItemId == definitionId).GetComponent<Button>().onClick.Invoke();
        }

        private static void ClickM18Action(string name)
        {
            var button = Object.FindFirstObjectByType<DecorationActionBarView>()
                .GetComponentsInChildren<Button>(true).Single(item => item.name == name);
            Assert.That(button.gameObject.activeInHierarchy && button.interactable, Is.True, name);
            button.onClick.Invoke();
        }

        private void InvokeM18Controller(string name, params object[] args)
        {
            var method = typeof(DecorationModeController).GetMethod(name,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, name);
            method.Invoke(m18Controller, args);
        }

        private static M18ExpectedAnchor[] M18Baseline() => new[]
        {
            new M18ExpectedAnchor(LayoutStationType.CashRegister, InteractionRole.Employee, 2, 4, CardinalDirection.South),
            new M18ExpectedAnchor(LayoutStationType.CashRegister, InteractionRole.Customer, 2, 2, CardinalDirection.North),
            new M18ExpectedAnchor(LayoutStationType.CoffeeMachine, InteractionRole.Employee, 4, 4, CardinalDirection.South),
            new M18ExpectedAnchor(LayoutStationType.PickUpPoint, InteractionRole.Employee, 6, 4, CardinalDirection.South),
            new M18ExpectedAnchor(LayoutStationType.PickUpPoint, InteractionRole.Customer, 6, 2, CardinalDirection.North)
        };

        private static Transform[] M18Markers() =>
            Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(item => item.name.StartsWith("AnchorDebug_", System.StringComparison.Ordinal)).ToArray();

        private void AssertM18Markers(M18ExpectedAnchor[] expected)
        {
            var root = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Single(item => item.name == "InteractionAnchorDebugRoot");
            var gridRootField = typeof(DecorationModeController).GetField("gridRoot",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(gridRootField, Is.Not.Null);
            var gridRoot = (Transform)gridRootField.GetValue(m18Controller);
            Assert.That(gridRoot, Is.Not.Null);
            var markers = M18Markers();
            Assert.That(markers, Has.Length.EqualTo(expected.Length), "No missing, duplicate or stale marker objects.");
            Assert.That(markers.Select(marker => marker.name).Distinct().Count(), Is.EqualTo(expected.Length));
            Assert.That(root.childCount, Is.EqualTo(expected.Length), "No unnamed leftover children under debug root.");
            Assert.That(m18Runtime.CurrentReadiness.Stations, Has.Count.EqualTo(3));
            foreach (var group in expected.GroupBy(item => item.Type))
                Assert.That(m18Runtime.CurrentReadiness.Stations.Single(station => station.FunctionType == group.Key)
                    .Anchors.Anchors, Has.Count.EqualTo(group.Count()));
            foreach (var want in expected)
            {
                var station = m18Runtime.CurrentReadiness.Stations.Single(item => item.FunctionType == want.Type);
                Assert.That(station.Anchors.TryGetAnchor(want.Role, out var anchor), Is.True);
                Assert.That(anchor.Position, Is.EqualTo(new GridPosition(want.X, want.Y)));
                Assert.That(anchor.Facing, Is.EqualTo(want.Facing));
                var name = $"AnchorDebug_{want.Role}_{station.InstanceId}";
                var marker = markers.Single(item => item.name == name);
                Assert.That(marker.parent, Is.SameAs(root), name);
                Assert.That(marker.gameObject.activeInHierarchy, Is.True, name + " must be visible.");
                var local = new Vector3(want.X + .5f, .08f, want.Y + .5f);
                // Use the actual floor grid, not the debug root: a shifted root must fail too.
                Assert.That(Vector3.Distance(marker.position, gridRoot.TransformPoint(local)),
                    Is.LessThan(.001f), name + " world position");
                Assert.That(Quaternion.Angle(marker.rotation,
                    gridRoot.rotation * Quaternion.Euler(0f, (int)want.Facing * 90f, 0f)),
                    Is.LessThan(.01f), name + " world facing");
                var renderer = marker.GetComponent<MeshRenderer>();
                Assert.That(renderer, Is.Not.Null, name);
                Assert.That(renderer.enabled, Is.True);
                Assert.That(marker.GetComponent<MeshFilter>().sharedMesh, Is.Not.Null);
                var color = renderer.sharedMaterial.GetColor("_BaseColor");
                var expectedColor = want.Role == InteractionRole.Employee
                    ? new Color(.22f, .74f, .95f, 1f) : new Color(.98f, .66f, .20f, 1f);
                Assert.That(color.a, Is.EqualTo(1f).Within(.001f));
                Assert.That(color.r, Is.EqualTo(expectedColor.r).Within(.025f), name + " red channel");
                Assert.That(color.g, Is.EqualTo(expectedColor.g).Within(.025f), name + " green channel");
                Assert.That(color.b, Is.EqualTo(expectedColor.b).Within(.025f), name + " blue channel");
            }
        }

        private static System.Action InjectM18MarkerFault(string fault)
        {
            var marker = M18Markers().First(item => item.name.StartsWith("AnchorDebug_Employee_"));
            if (fault == "root-offset")
            {
                var root = marker.parent;
                var position = root.position;
                root.position += Vector3.right;
                return () => root.position = position;
            }
            if (fault == "root-rotation")
            {
                var root = marker.parent;
                var rotation = root.rotation;
                root.rotation *= Quaternion.Euler(0f, 90f, 0f);
                return () => root.rotation = rotation;
            }
            if (fault == "wrong-position")
            {
                var position = marker.localPosition;
                marker.localPosition += Vector3.right;
                return () => marker.localPosition = position;
            }
            if (fault == "wrong-color")
            {
                var renderer = marker.GetComponent<MeshRenderer>();
                var material = renderer.sharedMaterial;
                renderer.sharedMaterial = M18Markers().First(item => item.name.StartsWith("AnchorDebug_Customer_"))
                    .GetComponent<MeshRenderer>().sharedMaterial;
                return () => renderer.sharedMaterial = material;
            }
            if (fault == "missing")
            {
                marker.gameObject.SetActive(false);
                return () => marker.gameObject.SetActive(true);
            }
            if (fault == "duplicate" || fault == "stale")
            {
                var copy = Object.Instantiate(marker.gameObject, marker.parent);
                copy.name = fault == "duplicate" ? marker.name : marker.name + "_stale";
                if (fault == "stale") copy.transform.localPosition += Vector3.back * 2f;
                // Immediate cleanup belongs only to this test-created view, not production assets.
                return () => Object.DestroyImmediate(copy);
            }
            throw new System.ArgumentException("Unknown M18 negative probe: " + fault);
        }

        private readonly struct M18ExpectedAnchor
        {
            public readonly LayoutStationType Type;
            public readonly InteractionRole Role;
            public readonly int X;
            public readonly int Y;
            public readonly CardinalDirection Facing;
            public M18ExpectedAnchor(LayoutStationType type, InteractionRole role, int x, int y, CardinalDirection facing)
            {
                Type = type;
                Role = role;
                X = x;
                Y = y;
                Facing = facing;
            }
        }

        private static FurnitureRotation Rotate(DecorationModeController controller)
        {
            Assert.That(controller.TryRotateFunctionalSurfacePreview(), Is.True);
            return controller.ActiveFunctionalSurfacePreview.Rotation;
        }
    }
}
#endif

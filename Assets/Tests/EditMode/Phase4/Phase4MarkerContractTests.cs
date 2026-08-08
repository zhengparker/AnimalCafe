using System;
using System.Reflection;
using AnimalCafe.Content;
using AnimalCafe.Layout;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AnimalCafe.Tests.Phase4
{
    public sealed class Phase4MarkerContractTests
    {
        [Test]
        public void SurfaceSlotMarker_ExposesStableLocalId()
        {
            var go = new GameObject("SurfaceSlot_0");
            try
            {
                var marker = go.AddComponent<SurfaceSlotMarker>();
                SetSerialized(marker, "slotId", "slot.0");

                Assert.That(marker.SlotId, Is.EqualTo("slot.0"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void CashRegisterSideMarker_ExposesAuthoredSideAndCardinalLocalDirection()
        {
            var go = new GameObject("CashRegisterEmployeeSide");
            try
            {
                var marker = go.AddComponent<CashRegisterSideMarker>();
                SetSerialized(marker, "sideType", CashRegisterSideType.Employee);
                SetSerialized(marker, "localDirection", CardinalDirection.North);

                Assert.That(marker.SideType, Is.EqualTo(CashRegisterSideType.Employee));
                Assert.That(marker.LocalDirection, Is.EqualTo(CardinalDirection.North));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void CashRegisterSideMarkers_ReadSidesFromRoot_ConvertsOneOppositePair()
        {
            var root = new GameObject("CashRegister");
            try
            {
                AddSideMarker(root, CashRegisterSideType.Employee, CardinalDirection.South);
                AddSideMarker(root, CashRegisterSideType.Customer, CardinalDirection.North);

                var sides = CashRegisterSideMarker.ReadSidesFrom(root);

                Assert.That(sides.EmployeeSide, Is.EqualTo(CardinalDirection.South));
                Assert.That(sides.CustomerSide, Is.EqualTo(CardinalDirection.North));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CashRegisterSideMarkers_ReadSidesFromRoot_DerivesQueueDirectionFromCustomerSide()
        {
            var root = new GameObject("CashRegister");
            try
            {
                AddSideMarker(root, CashRegisterSideType.Employee, CardinalDirection.West);
                AddSideMarker(root, CashRegisterSideType.Customer, CardinalDirection.East);

                var sides = CashRegisterSideMarker.ReadSidesFrom(root);

                Assert.That(sides.QueueDirection, Is.EqualTo(CardinalDirection.East));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CashRegisterSideMarkers_ReadSidesFromRoot_IncludesInactiveDescendantMarkers()
        {
            var root = new GameObject("CashRegister");
            try
            {
                AddSideMarker(root, CashRegisterSideType.Employee, CardinalDirection.South);
                var customerMarker = AddSideMarker(
                    root,
                    CashRegisterSideType.Customer,
                    CardinalDirection.North);
                customerMarker.gameObject.SetActive(false);

                var sides = CashRegisterSideMarker.ReadSidesFrom(root);

                Assert.That(sides.EmployeeSide, Is.EqualTo(CardinalDirection.South));
                Assert.That(sides.CustomerSide, Is.EqualTo(CardinalDirection.North));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CashRegisterSideMarkers_ReadSidesFromRoot_RejectsMissingSide()
        {
            var root = new GameObject("CashRegister");
            try
            {
                AddSideMarker(root, CashRegisterSideType.Employee, CardinalDirection.South);

                Assert.Throws<ArgumentException>(() =>
                    CashRegisterSideMarker.ReadSidesFrom(root));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CashRegisterSideMarkers_ReadSidesFromRoot_RejectsDuplicateSide()
        {
            var root = new GameObject("CashRegister");
            try
            {
                AddSideMarker(root, CashRegisterSideType.Employee, CardinalDirection.South);
                AddSideMarker(root, CashRegisterSideType.Employee, CardinalDirection.North);
                AddSideMarker(root, CashRegisterSideType.Customer, CardinalDirection.North);

                Assert.Throws<ArgumentException>(() =>
                    CashRegisterSideMarker.ReadSidesFrom(root));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CashRegisterSideMarkers_ReadSidesFromRoot_RejectsDuplicateCustomerSide()
        {
            var root = new GameObject("CashRegister");
            try
            {
                AddSideMarker(root, CashRegisterSideType.Employee, CardinalDirection.South);
                AddSideMarker(root, CashRegisterSideType.Customer, CardinalDirection.North);
                AddSideMarker(root, CashRegisterSideType.Customer, CardinalDirection.South);

                Assert.Throws<ArgumentException>(() =>
                    CashRegisterSideMarker.ReadSidesFrom(root));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [TestCase(CardinalDirection.North, CardinalDirection.North)]
        [TestCase(CardinalDirection.North, CardinalDirection.East)]
        public void CashRegisterSideMarkers_ReadSidesFromRoot_RejectsSameOrPerpendicularDirections(
            CardinalDirection employeeDirection,
            CardinalDirection customerDirection)
        {
            var root = new GameObject("CashRegister");
            try
            {
                AddSideMarker(root, CashRegisterSideType.Employee, employeeDirection);
                AddSideMarker(root, CashRegisterSideType.Customer, customerDirection);

                Assert.Throws<ArgumentException>(() =>
                    CashRegisterSideMarker.ReadSidesFrom(root));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CashRegisterSideMarkers_ReadSidesFromRoot_RejectsUndefinedDirection()
        {
            var root = new GameObject("CashRegister");
            try
            {
                AddSideMarker(root, CashRegisterSideType.Employee, (CardinalDirection)99);
                AddSideMarker(root, CashRegisterSideType.Customer, CardinalDirection.North);

                Assert.Throws<ArgumentOutOfRangeException>(() =>
                    CashRegisterSideMarker.ReadSidesFrom(root));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CashRegisterSideMarkers_ReadSidesFromRoot_RejectsUndefinedSideType()
        {
            var root = new GameObject("CashRegister");
            try
            {
                AddSideMarker(root, CashRegisterSideType.Employee, CardinalDirection.South);
                AddSideMarker(root, CashRegisterSideType.Customer, CardinalDirection.North);
                AddSideMarker(root, (CashRegisterSideType)99, CardinalDirection.East);

                Assert.Throws<ArgumentOutOfRangeException>(() =>
                    CashRegisterSideMarker.ReadSidesFrom(root));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void WallSurfaceAuthoring_ExposesReusableGridValuesWithoutProductionDefaults()
        {
            var go = new GameObject("WallSurface");
            try
            {
                var wall = go.AddComponent<WallSurfaceAuthoring>();
                SetSerialized(wall, "surfaceId", "wall.fixture");
                SetSerialized(wall, "columns", 3);
                SetSerialized(wall, "rows", 4);
                SetSerialized(wall, "slotSize", 0.5f);

                Assert.That(wall.SurfaceId, Is.EqualTo("wall.fixture"));
                Assert.That(wall.Columns, Is.EqualTo(3));
                Assert.That(wall.Rows, Is.EqualTo(4));
                Assert.That(wall.SlotSize, Is.EqualTo(0.5f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void WallSurfaceAuthoring_DefaultsToReusableSingleSlotValuesNotProductionEightByTwo()
        {
            var go = new GameObject("WallSurface");
            try
            {
                var wall = go.AddComponent<WallSurfaceAuthoring>();

                Assert.That(wall.Columns, Is.EqualTo(1));
                Assert.That(wall.Rows, Is.EqualTo(1));
                Assert.That(wall.SlotSize, Is.EqualTo(1f));
                Assert.That(wall.Columns, Is.Not.EqualTo(8));
                Assert.That(wall.Rows, Is.Not.EqualTo(2));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void FurnitureDefinitionAsset_FootprintFieldsExplainGridCellUnits()
        {
            var widthField = typeof(FurnitureDefinitionAsset).GetField(
                "footprintWidth",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var depthField = typeof(FurnitureDefinitionAsset).GetField(
                "footprintDepth",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(widthField, Is.Not.Null);
            Assert.That(depthField, Is.Not.Null);
            Assert.That(
                widthField.GetCustomAttribute<TooltipAttribute>()?.tooltip,
                Does.Contain("Grid cell"));
            Assert.That(
                depthField.GetCustomAttribute<TooltipAttribute>()?.tooltip,
                Does.Contain("Grid cell"));
        }

        [Test]
        public void WallSurfaceAuthoring_DefaultGizmoDepthIsVisibleOnInteriorSurface()
        {
            var go = new GameObject("WallSurface");
            try
            {
                var wall = go.AddComponent<WallSurfaceAuthoring>();
                var property = typeof(WallSurfaceAuthoring).GetProperty(
                    "GizmoDepthOffset",
                    BindingFlags.Instance | BindingFlags.Public);

                Assert.That(property, Is.Not.Null);
                Assert.That(
                    (float)property.GetValue(wall),
                    Is.EqualTo(-0.055f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void EntranceAuthoring_CreatesExactTwoByTwoReservation()
        {
            var go = new GameObject("Entrance");
            try
            {
                var portal = go.AddComponent<EntrancePortalAuthoring>();
                SetSerialized(portal, "entranceId", "entrance.main");
                SetSerialized(portal, "originX", 3);
                SetSerialized(portal, "originY", 0);

                var reservation = portal.CreateReservation();

                Assert.That(portal.EntranceId, Is.EqualTo("entrance.main"));
                Assert.That(reservation.Type, Is.EqualTo(LayoutReservationType.EntranceClearance));
                Assert.That(reservation.Origin, Is.EqualTo(new GridPosition(3, 0)));
                Assert.That(reservation.Size, Is.EqualTo(new GridSize(2, 2)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void WallMountedDefinitionAsset_ExposesPrefabAndExplicitWallFootprint()
        {
            var asset = ScriptableObject.CreateInstance<WallMountedDefinitionAsset>();
            var prefab = new GameObject("WindowPrefab");
            try
            {
                SetSerialized(asset, "definitionId", "window.basic.01");
                SetSerialized(asset, "displayName", "Basic Window");
                SetSerialized(asset, "footprintWidth", 1);
                SetSerialized(asset, "footprintHeight", 2);
                SetSerialized(asset, "prefab", prefab);

                Assert.That(asset.DefinitionId, Is.EqualTo("window.basic.01"));
                Assert.That(asset.DisplayName, Is.EqualTo("Basic Window"));
                Assert.That(asset.Footprint, Is.EqualTo(new WallFootprint(1, 2)));
                Assert.That(asset.Prefab, Is.SameAs(prefab));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
                UnityEngine.Object.DestroyImmediate(prefab);
            }
        }

        [TestCase(typeof(SurfaceSlotMarker))]
        [TestCase(typeof(CashRegisterSideMarker))]
        [TestCase(typeof(WallSurfaceAuthoring))]
        [TestCase(typeof(EntrancePortalAuthoring))]
        public void AuthoringMarkers_AddNoRendererColliderOrUpdateLoop(Type markerType)
        {
            var go = new GameObject(markerType.Name);
            try
            {
                go.AddComponent(markerType);

                Assert.That(go.GetComponent<Renderer>(), Is.Null);
                Assert.That(go.GetComponent<Collider>(), Is.Null);
                Assert.That(
                    markerType.GetMethod(
                        "Update",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
                    Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private static void SetSerialized(
            UnityEngine.Object target,
            string propertyName,
            object value)
        {
            var serializedTarget = new SerializedObject(target);
            var property = serializedTarget.FindProperty(propertyName);

            Assert.That(property, Is.Not.Null, $"Missing serialized property '{propertyName}'.");

            switch (property.propertyType)
            {
                case SerializedPropertyType.String:
                    property.stringValue = (string)value;
                    break;
                case SerializedPropertyType.Integer:
                case SerializedPropertyType.Enum:
                    property.intValue = Convert.ToInt32(value);
                    break;
                case SerializedPropertyType.Float:
                    property.floatValue = Convert.ToSingle(value);
                    break;
                case SerializedPropertyType.ObjectReference:
                    property.objectReferenceValue = (UnityEngine.Object)value;
                    break;
                default:
                    Assert.Fail($"Unsupported serialized property type '{property.propertyType}'.");
                    break;
            }

            serializedTarget.ApplyModifiedPropertiesWithoutUndo();
        }

        private static CashRegisterSideMarker AddSideMarker(
            GameObject root,
            CashRegisterSideType sideType,
            CardinalDirection localDirection)
        {
            var child = new GameObject(sideType.ToString());
            child.transform.SetParent(root.transform);
            var marker = child.AddComponent<CashRegisterSideMarker>();
            SetSerialized(marker, "sideType", sideType);
            SetSerialized(marker, "localDirection", localDirection);
            return marker;
        }
    }
}

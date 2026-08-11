using AnimalCafe.Layout;
using NUnit.Framework;

namespace AnimalCafe.Tests.Phase4
{
    public sealed class FurnitureFunctionContractTests
    {
        [Test]
        public void LegacyConstructor_DefaultsFunctionTypeToNone()
        {
            var definition = new FurnitureDefinition(
                "furniture.counter.module.01",
                "Counter",
                new GridSize(1, 1),
                PlacementSurfaceType.Floor);

            Assert.That(definition.FunctionType, Is.EqualTo(FurnitureFunctionType.None));
        }

        [Test]
        public void FiveArgumentConstructor_PreservesCashRegisterType()
        {
            var definition = new FurnitureDefinition(
                "equipment.cash-register.01",
                "Cash Register",
                new GridSize(1, 1),
                PlacementSurfaceType.FurnitureSurface,
                FurnitureFunctionType.CashRegister);

            Assert.That(definition.FunctionType, Is.EqualTo(FurnitureFunctionType.CashRegister));
        }

        [Test]
        public void FiveArgumentConstructor_RejectsUnknownFunctionType()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                new FurnitureDefinition(
                    "furniture.counter.module.01",
                    "Counter",
                    new GridSize(1, 1),
                    PlacementSurfaceType.Floor,
                    (FurnitureFunctionType)99));
        }

        [TestCase(CardinalDirection.North, FurnitureRotation.Degrees90, CardinalDirection.East)]
        [TestCase(CardinalDirection.East, FurnitureRotation.Degrees90, CardinalDirection.South)]
        public void Rotate_ReturnsExpectedCardinalDirection(
            CardinalDirection direction,
            FurnitureRotation rotation,
            CardinalDirection expected)
        {
            Assert.That(direction.Rotate(rotation), Is.EqualTo(expected));
        }

        [Test]
        public void CashRegisterSides_ExposesCustomerSideAsQueueDirection()
        {
            var sides = new CashRegisterSides(CardinalDirection.South, CardinalDirection.North);

            Assert.That(sides.QueueDirection, Is.EqualTo(CardinalDirection.North));
        }

        [Test]
        public void Rotate_InvalidFurnitureRotationThrows()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                CardinalDirection.North.Rotate((FurnitureRotation)45));
        }

        [Test]
        public void Rotate_InvalidCardinalDirectionThrows()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                ((CardinalDirection)4).Rotate(FurnitureRotation.Degrees90));
        }

        [TestCase(CardinalDirection.North, CardinalDirection.North)]
        [TestCase(CardinalDirection.North, CardinalDirection.East)]
        [TestCase(CardinalDirection.East, CardinalDirection.South)]
        public void CashRegisterSides_RejectsSameOrPerpendicularSides(
            CardinalDirection employeeSide,
            CardinalDirection customerSide)
        {
            Assert.Throws<System.ArgumentException>(() =>
                new CashRegisterSides(employeeSide, customerSide));
        }

        [Test]
        public void CashRegisterSides_RejectsUnknownDirection()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                new CashRegisterSides((CardinalDirection)4, CardinalDirection.North));
        }

        [Test]
        public void CashRegisterSides_RotateReturnsNewOppositeSides()
        {
            var sides = new CashRegisterSides(CardinalDirection.South, CardinalDirection.North);

            var rotated = sides.Rotate(FurnitureRotation.Degrees90);

            Assert.That(rotated, Is.Not.SameAs(sides));
            Assert.That(rotated.EmployeeSide, Is.EqualTo(CardinalDirection.West));
            Assert.That(rotated.CustomerSide, Is.EqualTo(CardinalDirection.East));
            Assert.That(sides.EmployeeSide, Is.EqualTo(CardinalDirection.South));
            Assert.That(sides.CustomerSide, Is.EqualTo(CardinalDirection.North));
        }

        [TestCase(FurnitureRotation.Degrees0, CardinalDirection.South, CardinalDirection.North)]
        [TestCase(FurnitureRotation.Degrees90, CardinalDirection.West, CardinalDirection.East)]
        [TestCase(FurnitureRotation.Degrees180, CardinalDirection.North, CardinalDirection.South)]
        [TestCase(FurnitureRotation.Degrees270, CardinalDirection.East, CardinalDirection.West)]
        public void CashRegisterSides_AllQuarterTurnsReturnLiteralExpectedSides(
            FurnitureRotation rotation,
            CardinalDirection expectedEmployeeSide,
            CardinalDirection expectedCustomerSide)
        {
            var sides = new CashRegisterSides(CardinalDirection.South, CardinalDirection.North);

            var rotated = sides.Rotate(rotation);

            Assert.That(rotated.EmployeeSide, Is.EqualTo(expectedEmployeeSide));
            Assert.That(rotated.CustomerSide, Is.EqualTo(expectedCustomerSide));
        }
    }
}

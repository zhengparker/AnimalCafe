using System;
using AnimalCafe.Layout;
using NUnit.Framework;

namespace AnimalCafe.Tests
{
    public sealed class GridValueTests
    {
        [Test]
        public void GridPosition_StoresCoordinates()
        {
            var position = new GridPosition(2, 3);

            Assert.That(position.X, Is.EqualTo(2));
            Assert.That(position.Y, Is.EqualTo(3));
        }

        [Test]
        public void GridPosition_EqualValuesCompareEqualAndShareHashCode()
        {
            var first = new GridPosition(2, 3);
            var second = new GridPosition(2, 3);

            Assert.That(first, Is.EqualTo(second));
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
        }

        [Test]
        public void GridPosition_DifferentValuesCompareUnequal()
        {
            Assert.That(new GridPosition(2, 3), Is.Not.EqualTo(new GridPosition(3, 2)));
        }

        [Test]
        public void GridPosition_NegativeCoordinatesConstructSuccessfully()
        {
            var position = new GridPosition(-2, -3);

            Assert.That(position.X, Is.EqualTo(-2));
            Assert.That(position.Y, Is.EqualTo(-3));
        }

        [TestCase(1, 1)]
        [TestCase(2, 3)]
        public void GridSize_ValidDimensionsConstructSuccessfully(int width, int height)
        {
            var size = new GridSize(width, height);

            Assert.That(size.Width, Is.EqualTo(width));
            Assert.That(size.Height, Is.EqualTo(height));
        }

        [Test]
        public void GridSize_EqualValuesCompareEqualAndShareHashCode()
        {
            var first = new GridSize(2, 3);
            var second = new GridSize(2, 3);

            Assert.That(first, Is.EqualTo(second));
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
        }

        [TestCase(0, 1)]
        [TestCase(1, 0)]
        [TestCase(-1, 1)]
        [TestCase(1, -1)]
        public void GridSize_InvalidDimensionThrows(int width, int height)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new GridSize(width, height));
        }

        [Test]
        public void GridSettings_ValidCellSizeConstructsSuccessfully()
        {
            var settings = new GridSettings(1f);

            Assert.That(settings.CellSize, Is.EqualTo(1f));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void GridSettings_InvalidCellSizeThrows(float cellSize)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new GridSettings(cellSize));
        }

        [TestCase(FurnitureRotation.Degrees0, 2, 3)]
        [TestCase(FurnitureRotation.Degrees90, 3, 2)]
        [TestCase(FurnitureRotation.Degrees180, 2, 3)]
        [TestCase(FurnitureRotation.Degrees270, 3, 2)]
        public void GridSize_RotationReturnsExpectedSize(
            FurnitureRotation rotation,
            int expectedWidth,
            int expectedHeight)
        {
            var rotated = new GridSize(2, 3).Rotate(rotation);

            Assert.That(rotated.Width, Is.EqualTo(expectedWidth));
            Assert.That(rotated.Height, Is.EqualTo(expectedHeight));
        }

        [Test]
        public void GridSize_InvalidRotationThrows()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new GridSize(2, 3).Rotate((FurnitureRotation)45));
        }

        [Test]
        public void GridSize_FourSuccessiveNinetyDegreeRotationsReturnOriginalSize()
        {
            var size = new GridSize(2, 3);

            for (var rotationCount = 0; rotationCount < 4; rotationCount++)
            {
                size = size.Rotate(FurnitureRotation.Degrees90);
            }

            Assert.That(size, Is.EqualTo(new GridSize(2, 3)));
        }
    }
}

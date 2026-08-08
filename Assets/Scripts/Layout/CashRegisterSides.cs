using System;

namespace AnimalCafe.Layout
{
    public sealed class CashRegisterSides
    {
        public CardinalDirection EmployeeSide { get; }
        public CardinalDirection CustomerSide { get; }
        public CardinalDirection QueueDirection => CustomerSide;

        public CashRegisterSides(
            CardinalDirection employeeSide,
            CardinalDirection customerSide)
        {
            if (!Enum.IsDefined(typeof(CardinalDirection), employeeSide))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(employeeSide),
                    employeeSide,
                    "Employee side must be a defined cardinal direction.");
            }

            if (!Enum.IsDefined(typeof(CardinalDirection), customerSide))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(customerSide),
                    customerSide,
                    "Customer side must be a defined cardinal direction.");
            }

            if (employeeSide.Rotate(FurnitureRotation.Degrees180) != customerSide)
            {
                throw new ArgumentException(
                    "Customer side must be opposite the employee side.",
                    nameof(customerSide));
            }

            EmployeeSide = employeeSide;
            CustomerSide = customerSide;
        }

        public CashRegisterSides Rotate(FurnitureRotation rotation)
        {
            return new CashRegisterSides(
                EmployeeSide.Rotate(rotation),
                CustomerSide.Rotate(rotation));
        }
    }
}

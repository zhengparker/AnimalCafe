using System;
using System.Collections.Generic;
using AnimalCafe.Layout;

namespace AnimalCafe.Content
{
    public sealed class FunctionalDirectionCatalog
    {
        private readonly Dictionary<string, CashRegisterSides> cashRegisterSidesByDefinition;
        private readonly Dictionary<string, CardinalDirection> coffeeMachineDirectionsByDefinition;

        public FunctionalDirectionCatalog(
            IDictionary<string, CashRegisterSides> cashRegisterSides,
            IDictionary<string, CardinalDirection> coffeeMachineDirections)
        {
            if (cashRegisterSides == null)
            {
                throw new ArgumentNullException(nameof(cashRegisterSides));
            }

            if (coffeeMachineDirections == null)
            {
                throw new ArgumentNullException(nameof(coffeeMachineDirections));
            }

            cashRegisterSidesByDefinition =
                new Dictionary<string, CashRegisterSides>(StringComparer.Ordinal);
            coffeeMachineDirectionsByDefinition =
                new Dictionary<string, CardinalDirection>(StringComparer.Ordinal);

            foreach (var pair in cashRegisterSides)
            {
                FurnitureDefinition.ValidateDefinitionId(pair.Key, nameof(cashRegisterSides));
                if (pair.Value == null)
                {
                    throw new ArgumentException(
                        $"Cash Register direction '{pair.Key}' must not be null.",
                        nameof(cashRegisterSides));
                }

                cashRegisterSidesByDefinition.Add(pair.Key, pair.Value);
            }

            foreach (var pair in coffeeMachineDirections)
            {
                FurnitureDefinition.ValidateDefinitionId(pair.Key, nameof(coffeeMachineDirections));
                if (!Enum.IsDefined(typeof(CardinalDirection), pair.Value))
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(coffeeMachineDirections),
                        pair.Value,
                        $"Coffee Machine direction '{pair.Key}' must be a defined cardinal direction.");
                }

                if (cashRegisterSidesByDefinition.ContainsKey(pair.Key))
                {
                    throw new ArgumentException(
                        $"Functional definition '{pair.Key}' cannot be both a Cash Register and Coffee Machine.",
                        nameof(coffeeMachineDirections));
                }

                coffeeMachineDirectionsByDefinition.Add(pair.Key, pair.Value);
            }
        }

        public bool TryGetCashRegisterSides(
            string definitionId,
            out CashRegisterSides sides)
        {
            FurnitureDefinition.ValidateDefinitionId(definitionId, nameof(definitionId));
            return cashRegisterSidesByDefinition.TryGetValue(definitionId, out sides);
        }

        public bool TryGetCoffeeMachineDirection(
            string definitionId,
            out CardinalDirection direction)
        {
            FurnitureDefinition.ValidateDefinitionId(definitionId, nameof(definitionId));
            return coffeeMachineDirectionsByDefinition.TryGetValue(definitionId, out direction);
        }
    }
}

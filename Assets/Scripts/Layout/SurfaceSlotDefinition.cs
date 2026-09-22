using System;

namespace AnimalCafe.Layout
{
    public sealed class SurfaceSlotDefinition
    {
        public string SupportDefinitionId { get; }
        public string SlotId { get; }
        public GridPosition LocalCell { get; }

        public SurfaceSlotDefinition(
            string supportDefinitionId,
            string slotId,
            GridPosition localCell)
        {
            FurnitureDefinition.ValidateDefinitionId(
                supportDefinitionId,
                nameof(supportDefinitionId));
            LayoutStableId.Validate(slotId, nameof(slotId));

            SupportDefinitionId = supportDefinitionId;
            SlotId = slotId;
            LocalCell = localCell;
        }
    }
}

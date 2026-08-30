using AnimalCafe.Layout;
using UnityEngine;

namespace AnimalCafe.Content
{
    /// <summary>Bridges a pre-authored scene object into the runtime wall layout.</summary>
    public sealed class WallMountedSeedAuthoring : MonoBehaviour
    {
        [SerializeField] private string instanceId;
        [SerializeField] private string definitionId;
        [SerializeField] private string surfaceId;
        [SerializeField, Min(0)] private int column;
        [SerializeField, Min(0)] private int row;
        [SerializeField, Min(1)] private int footprintWidth = 1;
        [SerializeField, Min(1)] private int footprintHeight = 1;

        public string InstanceId => instanceId;
        public string DefinitionId => definitionId;
        public string SurfaceId => surfaceId;
        public WallSlotPosition Position => new WallSlotPosition(column, row);
        public WallFootprint Footprint => new WallFootprint(footprintWidth, footprintHeight);
    }
}

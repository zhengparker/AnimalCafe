using AnimalCafe.Capacity;

namespace AnimalCafe.EditorTools.Phase10
{
    internal sealed class CapacityDebugSession
    {
        internal CapacityService Service { get; private set; } = new CapacityService(64);

        internal void Reset()
        {
            // 临时容量只属于本窗口 / temporary capacity belongs to this window.
            Service = new CapacityService(64);
        }
    }
}

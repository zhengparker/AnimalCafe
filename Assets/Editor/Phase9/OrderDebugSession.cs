using AnimalCafe.Orders;

namespace AnimalCafe.EditorTools.Phase9
{
    internal sealed class OrderDebugSession
    {
        internal OrderService Service { get; private set; }

        internal OrderDebugSession()
        {
            Service = new OrderService();
        }

        internal void Reset()
        {
            // 临时资料仅属于当前窗口；重置时换掉整份 service。
            // Temporary data belongs only to this window; reset replaces its service.
            Service = new OrderService();
        }
    }
}
